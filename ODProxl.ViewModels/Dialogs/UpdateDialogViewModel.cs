using ODProxl.EntityModels;
using Prism.Dialogs;
using Prism.Mvvm;
using Velopack;

namespace ODProxl.ViewModels.Dialogs
{
    public class UpdateDialogViewModel : BindableBase, IDialogAware
    {
        public string Title => "發現新版本";

        private string _newVersion = "未知版本";
        public string NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        public UpdateInfo? UpdateInfo { get; private set; }     // Velopack 用
        public UpdateManifest? UpdateManifest { get; private set; } // CN 用

        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public UpdateDialogViewModel()
        {
            UpdateCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue<UpdateInfo>("UpdateInfo", out var info))
            {
                UpdateInfo = info;
                NewVersion = info.TargetFullRelease?.Version?.ToString() ?? "未知版本";
            }
            else if (parameters.TryGetValue<string>("NewVersion", out var ver))
            {
                NewVersion = ver;
            }
        }

        #region IDialogAware
        public DialogCloseListener RequestClose { get; set; }
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        #endregion
    }
}