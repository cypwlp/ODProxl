using ODProxl.EntityModels;
using ODProxl.Services;
using ODProxl.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ODProxl.Services.impls
{
    public class UploadService : IUploadService
    {
        public async Task PushUploadAsync(string version, string description, string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("上傳檔案不存在", filePath);

            string rid = PlatformHelper.GetCurrentRid();
            string baseUrl = $"http://129.204.149.106:8080/ODProxl/{rid}/";

            var manifest = new UpdateManifest
            {
                Version = version,
                Description = description,
                Files = new List<UpdateFile>()
            };

            using var client = new HttpClient();

            if (Path.GetExtension(filePath).ToLower() == ".zip")
            {
                using var archive = ZipFile.OpenRead(filePath);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;

                    var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    entry.ExtractToFile(tempFile, true);

                    var hash = await ComputeSha256Async(tempFile);
                    var size = new FileInfo(tempFile).Length;

                    manifest.Files.Add(new UpdateFile
                    {
                        Path = entry.FullName.Replace("\\", "/"),
                        Hash = hash,
                        Size = size
                    });

                    await UploadFileAsync(client, baseUrl + entry.FullName.Replace("\\", "/"), tempFile, cancellationToken);
                    File.Delete(tempFile);
                }
            }
            else
            {
                // 單一檔案 (.exe / .dll)
                var hash = await ComputeSha256Async(filePath);
                manifest.Files.Add(new UpdateFile
                {
                    Path = Path.GetFileName(filePath),
                    Hash = hash,
                    Size = new FileInfo(filePath).Length
                });

                await UploadFileAsync(client, baseUrl + Path.GetFileName(filePath), filePath, cancellationToken);
            }

            // 上傳 manifest.json
            string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            var manifestContent = new StringContent(manifestJson, Encoding.UTF8, "application/json");
            await client.PutAsync(baseUrl + "manifest.json", manifestContent, cancellationToken);
        }

        public async Task CreateAndPushGitTagAsync(string version, string description, string repoPath)
        {
            if (!Directory.Exists(repoPath))
                throw new DirectoryNotFoundException($"Git 專案路徑不存在：{repoPath}");

            var tagName = $"v{version}";

            // 先刪除本地舊 tag（避免已存在）
            await RunGitCommandAsync(repoPath, $"tag -d \"{tagName}\" 2>nul || true");

            // 建立 annotated tag
            await RunGitCommandAsync(repoPath, $"tag -a \"{tagName}\" -m \"{description.Replace("\"", "\\\"")}\"");

            // push tag（觸發 GitHub Actions）
            await RunGitCommandAsync(repoPath, $"push origin \"{tagName}\"");
        }

        private static async Task RunGitCommandAsync(string workingDirectory, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 git");
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"git 執行失敗\nCommand: {arguments}\nError: {error}\nOutput: {output}");
        }

        private static async Task<string> ComputeSha256Async(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var bytes = await sha.ComputeHashAsync(fs);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private static async Task UploadFileAsync(HttpClient client, string url, string localPath, CancellationToken ct)
        {
            using var fs = File.OpenRead(localPath);
            using var content = new StreamContent(fs);
            await client.PutAsync(url, content, ct);
        }
    }
}