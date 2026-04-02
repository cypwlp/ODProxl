using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ODProxl.EntityModels;
using ODProxl.Services;
using Velopack;

namespace ODProxl.ViewModels.Dialogs
{
    public class UpdateDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IUpdateService _updateService;
        private bool _isClosing = false;
        private string? _countryCode;

        public UpdateDialogViewModel(IUpdateService updateService)
        {
            _updateService = updateService;
            UpdateCommand = new DelegateCommand(OnUpdate);
            CancelCommand = new DelegateCommand(OnCancel);
            ProgressReporter = new Progress<UpdateProgress>(OnProgressReport);
        }

        public string Title => "軟體更新";

        private string _dialogTitle = "發現新版本";
        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        private string _newVersion = "未知版本";
        public string NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        private string _updateMessage = string.Empty;
        public string UpdateMessage
        {
            get => _updateMessage;
            set => SetProperty(ref _updateMessage, value);
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private string _progressText = "";
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        private bool _isUpdating = false;
        public bool IsUpdating
        {
            get => _isUpdating;
            set => SetProperty(ref _isUpdating, value);
        }

        public IProgress<UpdateProgress> ProgressReporter { get; }
        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private async void OnUpdate()
        {
            if (IsUpdating) return;
            IsUpdating = true;
            try
            {
                await _updateService.UpdateODProxlAsync(_countryCode, ProgressReporter);
            }
            catch (Exception ex)
            {
                ProgressReporter.Report(new UpdateProgress { Percentage = -1, StatusText = $"更新失敗：{ex.Message}" });
            }
        }

        private void OnCancel()
        {
            if (_isClosing) return;
            _isClosing = true;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    RequestClose.Invoke(ButtonResult.Cancel);
                }
                catch
                {
                    ForceCloseDialog();
                }
            }, DispatcherPriority.Background);
        }

        // ====================== 【重點修正】不再自動開始更新 ======================
        public void OnDialogOpened(IDialogParameters parameters)
        {
            _countryCode = parameters.GetValue<string>("CountryCode");
            if (string.IsNullOrEmpty(_countryCode))
            {
                OnCancel();
                return;
            }

            // 預設顯示「發現新版本」畫面，讓使用者自己點「立即更新」
            DialogTitle = "發現新版本";
            UpdateMessage = "已偵測到新版本可用，請問是否立即更新？";
            IsUpdating = false;
            ProgressValue = 0;
            ProgressText = "";

            // 如果有帶 UpdateInfo 或 DllUpdateList，顯示更詳細資訊
            if (parameters.TryGetValue<UpdateInfo>("UpdateInfo", out var info))
            {
                NewVersion = info.TargetFullRelease?.Version?.ToString() ?? "未知版本";
                UpdateMessage = $"發現新版本 {NewVersion}，是否立即更新？";
            }
            else if (parameters.TryGetValue<List<DllInfo>>("DllUpdateList", out var dllList) &&
                     parameters.TryGetValue<string>("Version", out var version))
            {
                NewVersion = version;
                DialogTitle = "DLL 更新";
                UpdateMessage = $"發現 {dllList.Count} 個 DLL 檔案需要更新\n\n此更新與主程式更新完全獨立。";
            }
        }
        // =========================================================================

        private void OnProgressReport(UpdateProgress progress)
        {
            if (_isClosing) return;
            if (progress.Percentage >= 0)
            {
                ProgressValue = progress.Percentage;
                IsIndeterminate = false;
            }
            else
            {
                IsIndeterminate = true;
            }
            ProgressText = progress.StatusText;
        }

        private void ForceCloseDialog()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    var window = desktopLifetime.Windows.FirstOrDefault(w => w.DataContext == this);
                    window?.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"強制關閉視窗失敗: {ex.Message}");
            }
        }

        public DialogCloseListener RequestClose { get; set; }
        public bool CanCloseDialog() => !IsUpdating;
        public void OnDialogClosed() { }
    }
}