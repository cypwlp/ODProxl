using Prism.Mvvm;   // 假設你使用 Prism，否則改用 CommunityToolkit.Mvvm
using Prism.Navigation;

namespace ODProxl.ViewModels.Pages
{
    public class UserPreferencePageViewModel : BindableBase, INavigationAware
    {
        #region INavigationAware
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public void OnNavigatedTo(NavigationContext navigationContext) { }
        #endregion

        // 開發者模式（建構子自動設定）
        private bool _isDeveloperMode;
        public bool IsDeveloperMode
        {
            get => _isDeveloperMode;
            set => SetProperty(ref _isDeveloperMode, value);
        }

        // 開發者設定屬性（你可以繼續新增）
        private bool _enableVerboseLogging;
        public bool EnableVerboseLogging
        {
            get => _enableVerboseLogging;
            set => SetProperty(ref _enableVerboseLogging, value);
        }

        private bool _enablePerformanceMonitoring;
        public bool EnablePerformanceMonitoring
        {
            get => _enablePerformanceMonitoring;
            set => SetProperty(ref _enablePerformanceMonitoring, value);
        }

        private bool _showDebugInfo;
        public bool ShowDebugInfo
        {
            get => _showDebugInfo;
            set => SetProperty(ref _showDebugInfo, value);
        }

        private bool _bypassProductionChecks;
        public bool BypassProductionChecks
        {
            get => _bypassProductionChecks;
            set => SetProperty(ref _bypassProductionChecks, value);
        }

        public UserPreferencePageViewModel()
        {
#if DEBUG
            IsDeveloperMode = true;
#else
            IsDeveloperMode = false;
#endif
        }

        // TODO: 加入 SaveCommand = new DelegateCommand(SavePreferences);
    }
}