using Avalonia;
using Avalonia.Threading;
using DryIoc;
using ODProxl.EntityModels;
using Prism.Dialogs;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ODProxl.Services.impls
{
    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;

        // DryIoc 會自動注入 IDialogService
        public UpdateService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        private string GetLocalVersion()
        {
            return "1.0.0";
        }


        private bool VerifyHash(string localPath, string expectedHash)
        {
            return true;

        }

        private async Task DownloadFilesAsync()
        {

        }
        private async Task<UpdateManifest> FetchManifestAsync(string baseUrl)
        {
            string manifestUrl = baseUrl.TrimEnd('/') + "/manifest.json";
            using var client = new HttpClient();
            // 设置超时，避免长时间阻塞
            client.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var response = await client.GetAsync(manifestUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
                return manifest;
            }
            catch (Exception ex)
            {
                // 处理网络异常、JSON 解析失败等，可根据需要抛出或返回 null
                throw new InvalidOperationException($"无法获取更新清单：{ex.Message}", ex);
            }
        }
        public async Task UpdateODProxlAsync(string countryCode)
        {
            if (countryCode == "CN")
            {
                string baseUrl = "http://129.204.149.106:8080/ODProxl/";
                string localAppPath = AppContext.BaseDirectory;
                string tempUpdatePath = Path.Combine(Path.GetTempPath(), "ODProxl_Update_" + Guid.NewGuid());

                try
                {
                    // 获取远程清单
                    var manifest = await FetchManifestAsync(baseUrl);
                    var localVersion = GetLocalVersion(); // 你需要实现这个方法

                    // 如果版本相同，可以跳过更新（但也可以按文件比对，这里简化）
                    if (manifest.Version == localVersion)
                        return;

                    // 比对文件差异
                    var filesToUpdate = new List<UpdateFile>();
                    foreach (var file in manifest.Files)
                    {
                        string localPath = Path.Combine(localAppPath, file.Path);
                        if (!File.Exists(localPath) || !VerifyHash(localPath, file.Hash))
                        {
                            filesToUpdate.Add(file);
                        }
                    }

                    if (!filesToUpdate.Any()) return;

                    // 弹出确认对话框...
                    var result = await _dialogService.ShowDialogAsync("UpdateDialog");
                    // 下载文件...

                    // 启动更新程序...
                }
                catch (Exception ex)
                {
                    // 错误处理
         
                }
            }

            else
            {
                // Velopack (國際版)
                var source = new GithubSource("https://github.com/cypwlp/ODProxl.git", "", false);
                var mgr = new UpdateManager(source);
                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters
                    {
                    { "UpdateInfo", updateInfo }
                    };

                    var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);

                    if (result?.Result == ButtonResult.OK)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
        }


    }
}