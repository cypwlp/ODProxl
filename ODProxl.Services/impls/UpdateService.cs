using Avalonia.Threading;
using ODProxl.EntityModels;
using ODProxl.Utils;
using Prism.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using File = System.IO.File;

namespace ODProxl.Services.impls
{
    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;
        private readonly HttpClient _httpClient;

        public UpdateService(IDialogService dialogService, HttpClient httpClient)
        {
            _dialogService = dialogService;
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ODProxl/UpdateService");
        }

        public async Task UpdateODProxlAsync(string countryCode, IProgress<UpdateProgress>? progress = null)
        {
            if (progress != null)
            {
                if (countryCode == "CN")
                    await DLLUpdateAsync(progress);
                else
                    await CheckAndUpdateForGitHubAsync(progress);
            }
            else
            {
                var parameters = new DialogParameters { { "CountryCode", countryCode } };
                await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
            }
        }

        public async Task<bool> PublishNewDllVersionAsync(string version, string dllFilePaths,
            string updateDescription, string codeDescription, string targetRid)
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(targetRid))
            {
                Console.WriteLine("[DLL Upload] 版本號或目標平台不能為空");
                return false;
            }
            string baseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/DLLUpdater/{targetRid}/";
            string manifestUrl = $"{baseUrl}dlls.json";
            DllManifest manifest = await GetOrCreateManifestAsync(manifestUrl);
            var paths = dllFilePaths.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            string localManifestPath = paths.FirstOrDefault(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (localManifestPath != null && File.Exists(localManifestPath))
            {
                string json = await File.ReadAllTextAsync(localManifestPath);
                manifest = JsonSerializer.Deserialize<DllManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DllManifest();
                manifest.Version = version;
            }
            else
            {
                manifest.Version = version;
                foreach (var localPath in paths.Where(File.Exists))
                {
                    await ProcessAndUploadSingleDllAsync(localPath, manifest, baseUrl);
                }
            }
            manifest.UpdateDescription = updateDescription ?? string.Empty;
            manifest.CodeDescription = codeDescription ?? string.Empty;
            await UploadManifestAsync(manifestUrl, manifest);
            return true;
        }

        public async Task<bool> PublishVelopackVersionAsync(string version, string updateDescription, string codeDescription)
        {
            try
            {
                string workingDir = await GetGitRepositoryRootAsync();
                await RunGitCommandAsync(workingDir, "add .");
                await RunGitCommandAsync(workingDir, $"commit --allow-empty -m \"{codeDescription}\"");
                await RunGitCommandAsync(workingDir, "push");
                await RunGitCommandAsync(workingDir, $"tag -a {version} -m \"{updateDescription}\"");
                await RunGitCommandAsync(workingDir, $"push origin {version}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Velopack Git] 發布失敗：{ex.Message}");
                return false;
            }
        }

        public async Task<string> GetLatestVersionFromGitHubAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.github.com/repos/cypwlp/ODProxl/releases/latest");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                {
                    string ver = tag.GetString() ?? "";
                    return ver.StartsWith("v") ? ver.Substring(1) : ver;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLatestVersion] 失敗：{ex.Message}");
            }
            return string.Empty;
        }

        private void StartUpdaterAndExit()
        {
            string updaterPath = Path.Combine(AppContext.BaseDirectory, "ODProxlUpdater.exe");
            if (File.Exists(updaterPath))
            {
                Process.Start(new ProcessStartInfo(updaterPath) { UseShellExecute = true });
            }
            Environment.Exit(0);
        }
        private async Task DLLUpdateAsync(IProgress<UpdateProgress> progress)
        {
            string rid = PlatformHelper.GetCurrentRid();
            string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string dllUpdateBaseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/DLLUpdater/{rid}/";
            string manifestUrl = $"{dllUpdateBaseUrl}dlls.json";

            var response = await _httpClient.GetAsync(manifestUrl);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            var manifest = JsonSerializer.Deserialize<DllManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest?.Dlls == null || manifest.Dlls.Count == 0) return;

            // 构建需要更新的文件列表
            var updateList = new List<DllInfo>();
            foreach (var dll in manifest.Dlls)
            {
                string localPath = Path.Combine(baseDir, dll.FileName);
                if (File.Exists(localPath))
                {
                    string localHash = await ComputeFileHashAsync(localPath);
                    if (localHash.Equals(dll.Hash, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                updateList.Add(dll);
            }
            if (updateList.Count == 0) return;

            // 准备暂存目录
            string pendingDir = Path.Combine(baseDir, "PendingUpdate");
            Directory.CreateDirectory(pendingDir);

            long totalBytes = updateList.Sum(d => d.Size);
            long downloadedBytes = 0;
            var pendingFiles = new List<UpdateFile>();

            progress.Report(new UpdateProgress { Percentage = -1, StatusText = "准备下载更新文件..." });

            foreach (var dll in updateList)
            {
                string downloadUrl = string.IsNullOrEmpty(dll.Url) ? $"{dllUpdateBaseUrl}{dll.FileName}" : dll.Url;
                string tempFilePath = Path.Combine(pendingDir, dll.FileName);  // 直接保存到 PendingUpdate

                using var httpResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                httpResponse.EnsureSuccessStatusCode();
                long? contentLength = httpResponse.Content.Headers.ContentLength;
                using var contentStream = await httpResponse.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                byte[] buffer = new byte[8192];
                long bytesRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    bytesRead += read;
                    if (contentLength.HasValue && contentLength.Value > 0)
                    {
                        int filePercent = (int)(bytesRead * 100 / contentLength.Value);
                        int totalPercent = totalBytes > 0 ? (int)((downloadedBytes + bytesRead) * 100 / totalBytes) : -1;
                        progress.Report(new UpdateProgress
                        {
                            Percentage = totalPercent,
                            StatusText = $"正在下载 {dll.FileName} ({filePercent}%)",
                            CurrentFile = dll.FileName
                        });
                    }
                    else
                    {
                        progress.Report(new UpdateProgress
                        {
                            Percentage = -1,
                            StatusText = $"正在下载 {dll.FileName}...",
                            CurrentFile = dll.FileName
                        });
                    }
                }

                // 哈希校验
                if (!string.Equals(await ComputeFileHashAsync(tempFilePath), dll.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempFilePath);
                    throw new Exception($"文件 {dll.FileName} 哈希校验失败");
                }

                pendingFiles.Add(new UpdateFile
                {
                    Source = Path.Combine("PendingUpdate", dll.FileName),
                    Dest = dll.FileName
                });

                downloadedBytes += dll.Size;
            }

            // 生成 pending.json
            await PreparePendingUpdateAsync(manifest.Version, pendingFiles);

            // 报告最终进度，然后启动更新器并退出
            progress.Report(new UpdateProgress { Percentage = 100, StatusText = "下载完成，正在准备更新..." });
            await Task.Delay(500); // 让UI有机会显示最后一条消息
            StartUpdaterAndExit();
        }

        private async Task PreparePendingUpdateAsync(string version, List<UpdateFile> files)
        {
            string baseDir = AppContext.BaseDirectory;
            string pendingDir = Path.Combine(baseDir, "PendingUpdate");
            Directory.CreateDirectory(pendingDir);

            var pending = new PendingUpdate
            {
                Version = version,
                UpdateType = "DllOnly",
                Files = files,
                RestartArgs = Array.Empty<string>(),
                DeleteAfterApply = true
            };

            string json = JsonSerializer.Serialize(pending, new JsonSerializerOptions { WriteIndented = true });
            string pendingJsonPath = Path.Combine(pendingDir, "pending.json");
            await File.WriteAllTextAsync(pendingJsonPath, json);
        }
        private async Task CheckAndUpdateForGitHubAsync(IProgress<UpdateProgress> progress)
        {
            try
            {
                var source = new GithubSource("https://github.com/cypwlp/ODProxl", "", false);
                var mgr = new UpdateManager(source);
                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null) return;

                Action<int> velopackProgress = percent =>
                {
                    progress.Report(new UpdateProgress
                    {
                        Percentage = percent,
                        StatusText = $"正在下载更新包 ({percent}%)"
                    });
                };

                await mgr.DownloadUpdatesAsync(updateInfo, velopackProgress);
                progress.Report(new UpdateProgress { Percentage = 100, StatusText = "下载完成，正在重启..." });
                await RestartApplicationAsync();
            }
            catch (Exception ex)
            {
                progress.Report(new UpdateProgress { Percentage = -1, StatusText = $"更新失败：{ex.Message}" });
            }
        }

        private async Task<string> GetGitRepositoryRootAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --show-toplevel",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi) ?? throw new Exception("無法啟動 git 程序");
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    throw new Exception($"git rev-parse 失敗：{error}");
                string root = output.Trim().Replace('/', '\\');
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    throw new Exception("取得的路徑無效");
                return root;
            }
            catch (Exception ex)
            {
                string fallback = AppContext.BaseDirectory;
                for (int i = 0; i < 3; i++)
                {
                    fallback = Directory.GetParent(fallback)?.FullName ?? fallback;
                }
                return fallback;
            }
        }

        private async Task RunGitCommandAsync(string workingDir, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi) ?? throw new Exception("無法啟動 git 程序");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new Exception($"git {arguments} 失敗\n錯誤：{error}");
        }

        private async Task<DllManifest> GetOrCreateManifestAsync(string manifestUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(manifestUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<DllManifest>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DllManifest();
                }
            }
            catch { }
            return new DllManifest();
        }

        private async Task ProcessAndUploadSingleDllAsync(string localPath, DllManifest manifest, string baseUrl)
        {
            string fileName = Path.GetFileName(localPath);
            string hash = await ComputeFileHashAsync(localPath);
            long size = new FileInfo(localPath).Length;
            var dllInfo = new DllInfo
            {
                FileName = fileName,
                Hash = hash,
                Size = size,
                Url = string.Empty
            };
            manifest.Dlls.RemoveAll(d => d.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            manifest.Dlls.Add(dllInfo);
            await UploadFileAsync($"{baseUrl}{fileName}", localPath);
        }

        private async Task UploadFileAsync(string url, string localFilePath)
        {
            using var fileStream = File.OpenRead(localFilePath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        private async Task UploadManifestAsync(string url, DllManifest manifest)
        {
            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }


        private async Task RestartApplicationAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Task.Delay(500);
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = false
                    });
                }
                Environment.Exit(0);
            });
        }
    }
}