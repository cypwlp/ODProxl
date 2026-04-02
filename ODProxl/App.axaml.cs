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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
            containerRegistry.RegisterSingleton<IDataService>(provider => new DataService("http://www.topmix.net/dataservice/GetData.asmx"));

            containerRegistry.RegisterSingleton<HttpClient>(provider =>
            {
                var client = new HttpClient();
                client.BaseAddress = new Uri("http://interior.topmix.net/info/system/software/ODProxl/");
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes("Administrator:wingfat@790811"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
                return client;
            });

            containerRegistry.RegisterSingleton<INotificationService, NotificationService>();
            containerRegistry.Register<IGeoLocationService, GeoLocationService>();
            containerRegistry.Register<IDialogService, DialogService>();
            containerRegistry.Register<IUpdateService, UpdateService>();
            containerRegistry.Register<IOnnxModelAnalyzer, OnnxModelAnalyzer>();
            containerRegistry.Register<IOnnxModelInspector, OnnxModelInspector>();
            containerRegistry.RegisterDialog<UpdateDialog, UpdateDialogViewModel>();
            containerRegistry.RegisterDialog<AboutDialog, AboutDialogViewModel>();
            containerRegistry.RegisterDialog<UploadDialog, UploadDialogViewModel>();
            containerRegistry.RegisterDialog<InputDialog, InputDialogViewModel>("InputDialog");
            containerRegistry.RegisterForNavigation<OnnxModelMSPage, OnnxModelMSPageViewModel>();
            containerRegistry.RegisterForNavigation<OnnxModelClassPage, OnnxModelClassPageViewModel>();
            containerRegistry.RegisterForNavigation<UserPreferencePage, UserPreferencePageViewModel>();
            containerRegistry.RegisterForNavigation<ClassMarkPage, ClassMarkPageViewModel>();
        }

        private async Task CheckForUpdatesAsync()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return;
            }
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
                    var mainWin = Container.Resolve<MainWin>();
                    var vm = Container.Resolve<MainWinViewModel>();
                    vm.LoginInfo = result.Parameters.GetValue<LoginInfo>("LoginInfo");
                    mainWin.DataContext = vm;

                    var regionManager = Container.Resolve<IRegionManager>();
                    RegionManager.SetRegionManager(mainWin, regionManager);
                    mainWin.Show();
                    desktopLifetime.MainWindow = mainWin;

                    await vm.DefaultNavigateAsync();
                    _ = CheckForUpdatesAsync();     // 正式版才會檢查
                    splashWindow.Close();
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