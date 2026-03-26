using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ODProxl.EntityModels;
using ODProxl.Utils;
using Prism.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ODProxl.Services.impls
{
    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;

        public UpdateService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        //public async Task UpdateODProxlAsync(string countryCode)
        //{
        //    if (countryCode == "CN")
        //    {
        //        await UpdateForChinaAsync();
        //    }
        //    else
        //    {
        //        // 國際版 Velopack
        //        var source = new GithubSource("https://github.com/cypwlp/ODProxl.git", "", false);
        //        var mgr = new UpdateManager(source);
        //        var updateInfo = await mgr.CheckForUpdatesAsync();
        //        if (updateInfo == null) return;

        //        await Dispatcher.UIThread.InvokeAsync(async () =>
        //        {
        //            var parameters = new DialogParameters { { "UpdateInfo", updateInfo } };
        //            var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
        //            if (result?.Result == ButtonResult.OK)
        //            {
        //                await mgr.DownloadUpdatesAsync(updateInfo);
        //                mgr.ApplyUpdatesAndRestart(updateInfo);
        //            }
        //        });
        //    }
        //}

        public async Task UpdateODProxlAsync(string countryCode)
        {
            if (countryCode == "CN")
            {
                await UpdateForChinaAsync();
            }
            else
            {
                // 國際版 Velopack - Debug 模式下跳過，避免 NotInstalledException
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("[Debug 模式] 已跳過 Velopack 更新檢查");
                    return;
                }

                try
                {
                    var source = new GithubSource("https://github.com/cypwlp/ODProxl.git", "", false);
                    var mgr = new UpdateManager(source);
                    var updateInfo = await mgr.CheckForUpdatesAsync();

                    if (updateInfo == null)
                    {
                        Console.WriteLine("[Velopack] 目前已是最新版本");
                        return;
                    }

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var parameters = new DialogParameters { { "UpdateInfo", updateInfo } };
                        var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
                        if (result?.Result == ButtonResult.OK)
                        {
                            await mgr.DownloadUpdatesAsync(updateInfo);
                            mgr.ApplyUpdatesAndRestart(updateInfo);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Velopack 更新檢查失敗：{ex.Message}");
                }
            }
        }
        private async Task UpdateForChinaAsync()
        {
            string rid = PlatformHelper.GetCurrentRid();
            string baseUrl = $"http://129.204.149.106:8080/ODProxl/{rid}/";
            string localAppPath = AppContext.BaseDirectory;

            try
            {
                var manifest = await FetchManifestAsync(baseUrl);
                if (manifest?.Version == GetLocalVersion()) return;

                var filesToUpdate = GetFilesNeedUpdate(manifest!, localAppPath);
                if (filesToUpdate.Count == 0) return;

                var parameters = new DialogParameters
                {
                    { "NewVersion", manifest!.Version },
                    { "UpdateManifest", manifest },
                    { "Description", manifest.Description }
                };

                var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);

                if (result?.Result == ButtonResult.OK)
                {
                    await DownloadAndApplyUpdatesAsync(filesToUpdate, baseUrl, localAppPath);
                    RestartApplication();
                }
            }
            catch (Exception ex)
            {
                // TODO: 可加入日誌或 Toast 提示
                Console.WriteLine($"CN 更新檢查失敗：{ex.Message}");
            }
        }

        private async Task<UpdateManifest?> FetchManifestAsync(string baseUrl)
        {
            string manifestUrl = baseUrl.TrimEnd('/') + "/manifest.json";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(manifestUrl);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UpdateManifest>(json);
        }

        private string GetLocalVersion()
        {
            // 可改成讀取 AssemblyVersion 或本地檔案，這裡先簡單寫死（實際專案建議改成動態）
            return "1.0.0";
        }

        private bool VerifyHash(string localPath, string expectedHash)
        {
            if (!File.Exists(localPath) || string.IsNullOrEmpty(expectedHash)) return false;
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(localPath);
            var hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLower();
            return hash == expectedHash;
        }

        private List<UpdateFile> GetFilesNeedUpdate(UpdateManifest manifest, string localAppPath)
        {
            var list = new List<UpdateFile>();
            foreach (var file in manifest.Files)
            {
                string localPath = Path.Combine(localAppPath, file.Path ?? "");
                if (!File.Exists(localPath) || !VerifyHash(localPath, file.Hash ?? ""))
                {
                    list.Add(file);
                }
            }
            return list;
        }

        private async Task DownloadAndApplyUpdatesAsync(List<UpdateFile> filesToUpdate, string baseUrl, string localAppPath)
        {
            using var client = new HttpClient();
            var tempDir = Path.Combine(Path.GetTempPath(), "ODProxl_Update_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            foreach (var file in filesToUpdate)
            {
                string downloadUrl = baseUrl.TrimEnd('/') + "/" + (file.Path ?? "").Replace("\\", "/");
                string localTempPath = Path.Combine(tempDir, file.Path ?? "");

                Directory.CreateDirectory(Path.GetDirectoryName(localTempPath)!);
                var bytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(localTempPath, bytes);
            }

            // 覆蓋本地檔案
            foreach (var file in filesToUpdate)
            {
                string source = Path.Combine(tempDir, file.Path ?? "");
                string dest = Path.Combine(localAppPath, file.Path ?? "");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source, dest, true);
            }
              
            Directory.Delete(tempDir, true);
        }

        private void RestartApplication()
        {
            var currentProcess = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo
            {
                FileName = currentProcess.MainModule?.FileName,
                UseShellExecute = true,
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1))
            };

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
            Process.Start(startInfo);
            Environment.Exit(0);
        }
    }
}