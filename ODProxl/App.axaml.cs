using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ODProxl.Dialogs;
using ODProxl.Services;
using ODProxl.Services.impls;
using ODProxl.ViewModels;
using ODProxl.ViewModels.Dialogs;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;


namespace ODProxl
{
    public partial class App : PrismApplication
    {
        private IGeoLocationService _geoLocationService;
        protected override AvaloniaObject CreateShell() => null!;


        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<LoginDialog, LoginDialogViewModel>();
            containerRegistry.RegisterForNavigation<MainWin, MainWinViewModel>();
            containerRegistry.Register<IDataService, DataService>();
            containerRegistry.Register<IGeoLocationService, GeoLocationService>();
            //containerRegistry.Register<IDataService, DataService>();

        }

        private async Task CheckForUpdatesAsync()
        {
            string countryCode = null;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    countryCode = await _geoLocationService.GetCountryCodeAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {

                }
            }

            try
            {
                //bool useChinaMirror = countryCode == "CN";
                //IUpdateSource source;
                //if (useChinaMirror)
                //{
                //    source = new SimpleFileSource(new System.IO.DirectoryInfo("http://129.204.149.106:8080/releases"));
                //}
                //else
                //{
                //    source = new GithubSource("https://github.com/cypwlp/OB", "", false);
                //}
                var source = new GithubSource("https://github.com/cypwlp/OB", "", false);
                var mgr = new UpdateManager(source);
                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    return;
                }
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var dialogService = Container.Resolve<IDialogService>();
                    var parameters = new DialogParameters
            {
                { "UpdateInfo", updateInfo }
            };
                    var result = await dialogService.ShowDialogAsync("UpdateDialog", parameters);

                    if (result?.Result == ButtonResult.OK)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Velopack 更新檢查失敗: {ex.Message}");
            }
        }
        private async Task StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                SystemDecorations = SystemDecorations.None,
                ShowInTaskbar = false,
                Opacity = 0
            };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            var dialogService = Container.Resolve<IDialogService>();

            dialogService.ShowDialog("LoginDialog", null, async result =>
            {
                if (result?.Result == ButtonResult.OK)
                {
                    //if (result.Parameters.TryGetValue<RemoteDBTools>("dbtools", out var dbtools) &&
                    //    result.Parameters.TryGetValue<LogUserInfo>("LogUser", out var logUser))
                    //{
                        var mainWin = Container.Resolve<MainWin>();
                        var vm = Container.Resolve<MainWinViewModel>();
                        //vm.LogUser = logUser;
                        //vm.RemoteDBTools = dbtools;
                        mainWin.DataContext = vm;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;

                        await vm.DefaultNavigateAsync();

                        // 登入成功後立即檢查更新（最推薦的位置）
                        _ = CheckForUpdatesAsync();

                        splashWindow.Close();
                    //}
                    //else
                    //{
                    //    splashWindow.Close();
                    //    desktopLifetime.Shutdown();
                    //}
               }
                else
                {
                    splashWindow.Close();
                    desktopLifetime.Shutdown();
                }
            });
        }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                _ = StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}