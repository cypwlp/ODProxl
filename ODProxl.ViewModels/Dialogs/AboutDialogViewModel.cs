using Avalonia.Threading;
using DryIoc;
using Material.Icons;
using ODProxl.Services;
using ODProxl.Services.impls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ODProxl.ViewModels.Dialogs
{
    public class AboutDialogViewModel : BindableBase, IDialogAware
    {
        #region IDialogAware Implementation
        public string Title => "關於";
        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {

        }

        public void OnDialogOpened(IDialogParameters parameters)
        {

        }
        #endregion

        #region 字段
        private readonly IDialogService _dialogService;
        private readonly IUpdateService _updateService;
        private readonly IGeoLocationService _geoLocationService;
        private string countryCode;
        private string _currentVersion = "1.0.0";
        private bool _isChecking;
        public string UpdateButtonText => IsChecking ? "正在檢查..." : "檢查更新";
        public MaterialIconKind UpdateIcon => IsChecking ? MaterialIconKind.Refresh : MaterialIconKind.Update;
        public AboutDialogViewModel(IDialogService dialogService,IUpdateService updateService,IGeoLocationService geoLocationService)
        {
            _dialogService = dialogService;
            _updateService = updateService;
            _geoLocationService=geoLocationService;
            GetCurrentVersion();
            CheckUpdateCommand = new DelegateCommand(async () => await CheckForUpdatesInternalAsync());
        }
        #endregion


        #region 屬性
        public DelegateCommand CheckUpdateCommand { get; }

        public string CurrentVersion
        {
            get => _currentVersion;
            set => SetProperty(ref _currentVersion, value);
        }

        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                if (SetProperty(ref _isChecking, value))
                {
                    // 當狀態改變時，通知按鈕文字和圖標更新
                    RaisePropertyChanged(nameof(UpdateButtonText));
                    RaisePropertyChanged(nameof(UpdateIcon));
                }
            }
        }
        #endregion

        #region 檢查邏輯
        private  async Task CheckForUpdatesInternalAsync()
        {
            if (IsChecking) return;
            IsChecking = true;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                countryCode = await _geoLocationService.GetCountryCodeAsync(cts.Token);
                await _updateService.UpdateODProxlAsync(countryCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新檢查失敗: {ex.Message}");
            }
            finally
            {
                IsChecking = false;
            }
        }

        private void GetCurrentVersion()
        {
            if (countryCode == "CN")
            {
                var mgr = new UpdateManager(new SimpleWebSource("http://129.204.149.106:8080/ODProxl/"));
                CurrentVersion = mgr.CurrentVersion?.ToString() ?? "開發版本";
                return;
            }
            else
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/cypwlp/OB", "", false));
                CurrentVersion = mgr.CurrentVersion?.ToString() ?? "開發版本";
            }
        }
        #endregion
    }
}
