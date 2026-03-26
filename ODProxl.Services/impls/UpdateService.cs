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


        public async Task UpdateODProxlAsync(string countryCode)
        {
            if (countryCode == "CN")
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("[Debug 模式] 已跳過 Velopack 更新檢查");
                    return;
                }
                try {
                    var source = new Velopack.Sources.SimpleWebSource("http://129.204.149.106:8080/ODProxl/");
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
            else
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("[Debug 模式] 已跳過 Velopack 更新檢查");
                    return;
                }

                try
                {
  
                    var source = new GithubSource("https://github.com/cypwlp/ODProxl", "", false);
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
    }
}