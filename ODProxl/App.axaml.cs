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
using ODProxl.ViewModels.Pages;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using RemoteService;
using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;



namespace ODProxl
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell() => null!;


        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<LoginDialog, LoginDialogViewModel>();
            containerRegistry.RegisterForNavigation<MainWin, MainWinViewModel>();
            containerRegistry.RegisterForNavigation<HomePage, HomePageViewModel>();
            containerRegistry.RegisterSingleton<IDataService>(provider =>
    new DataService("http://www.topmix.net/dataservice/GetData.asmx"));
            containerRegistry.Register<IGeoLocationService, GeoLocationService>();
            containerRegistry.Register<IDialogService, DialogService>();
            containerRegistry.Register<IUpdateService, UpdateService>();
            containerRegistry.RegisterDialog<UpdateDialog, UpdateDialogViewModel>();
            containerRegistry.RegisterDialog<AboutDialog,AboutDialogViewModel>();
            containerRegistry.RegisterDialog<UploadDialog, UploadDialogViewModel>();
            containerRegistry.RegisterForNavigation<OnnxModelMSPage, OnnxModelMSPageViewModel>();
            containerRegistry.RegisterForNavigation<OnnxModelClassPage, OnnxModelClassPageViewModel>();
            containerRegistry.RegisterForNavigation<UserPreferencePage, UserPreferencePageViewModel>();
        }

        private async Task CheckForUpdatesAsync()
        {
            string countryCode;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var geoLocationService = Container.Resolve<IGeoLocationService>();
            countryCode = await geoLocationService.GetCountryCodeAsync(cts.Token);
            var updateService = Container.Resolve<IUpdateService>();
            await updateService.UpdateODProxlAsync(countryCode);
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
                        vm.LoginInfo= result.Parameters.GetValue<LoginInfo>("LoginInfo");
                        mainWin.DataContext = vm;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;

                        await vm.DefaultNavigateAsync();

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