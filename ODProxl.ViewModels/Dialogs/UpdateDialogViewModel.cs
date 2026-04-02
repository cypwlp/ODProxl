using ODProxl.EntityModels;
using Prism.Dialogs;
using Prism.Mvvm;
using Velopack;

namespace ODProxl.ViewModels.Dialogs
{
    public class UpdateDialogViewModel : BindableBase, IDialogAware
    {
        public string Title => "發現新版本";

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

        public bool IsDllUpdate { get; private set; } = false;

        public UpdateInfo? UpdateInfo { get; private set; }           // Velopack 主程式更新
        public List<DllInfo>? DllUpdateList { get; private set; }     // DLL 更新使用

        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public UpdateDialogViewModel()
        {
            UpdateCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // Velopack 主程式更新模式
            if (parameters.TryGetValue<UpdateInfo>("UpdateInfo", out var info))
            {
                IsDllUpdate = false;
                UpdateInfo = info;
                NewVersion = info.TargetFullRelease?.Version?.ToString() ?? "未知版本";
                DialogTitle = "發現新版本";
                UpdateMessage = $"新版本 {NewVersion} 已經準備就緒...";
            }
            // DLL 更新模式
            else if (parameters.TryGetValue<List<DllInfo>>("DllUpdateList", out var dllList) &&
                     parameters.TryGetValue<string>("Version", out var version))
            {
                IsDllUpdate = true;
                DllUpdateList = dllList;
                NewVersion = version;
                DialogTitle = "DLL 更新";
                UpdateMessage = $"發現 {dllList.Count} 個 DLL 檔案需要更新\n\n此更新與主程式更新完全獨立。";
            }
        }

        #region IDialogAware
        public DialogCloseListener RequestClose { get; set; }
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        #endregion
    }
}