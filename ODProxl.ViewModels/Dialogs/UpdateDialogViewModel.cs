using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Velopack;

namespace ODProxl.ViewModels.Dialogs
{
    public class UpdateDialogViewModel : BindableBase, IDialogAware
    {
        #region IDialogAware Implementation
        public DialogCloseListener RequestClose { get; set; }
        public bool CanCloseDialog()=>true;
        public void OnDialogClosed(){}
        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue<UpdateInfo>("UpdateInfo", out var info))
            {
                UpdateInfo = info;
            }
        }
        #endregion


        #region 字段
        public string Title => "發現新版本";
        private UpdateInfo? _updateInfo;
        public string NewVersion => UpdateInfo?.TargetFullRelease?.Version.ToString() ?? "未知版本";
        public UpdateDialogViewModel()
        {
            UpdateCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        #endregion


        #region 屬性
        public DelegateCommand? UpdateCommand { get; }
        public DelegateCommand? CancelCommand { get; }
        public UpdateInfo? UpdateInfo
        {
            get => _updateInfo;
            set => SetProperty(ref _updateInfo, value);
        }
        #endregion

    }
}
