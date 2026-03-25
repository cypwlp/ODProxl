using Avalonia.Logging;
using ODProxl.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Dialogs
{
    public class LoginDialogViewModel(IDialogService? dialogService, IDataService? dataService) : BindableBase,IDialogAware
    {
        #region IDialogAware Implementation
        public DialogCloseListener RequestClose { get; private set; }
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
        #endregion

        #region 字段
        private readonly IDialogService? _dialogService = dialogService;
        private readonly IDataService? _dataService = dataService;
        private RemoteService.LoginInfo? logInfo;
        private string? _userName;
        private string? _password;
        private string _database = "TopmixData";
        public DelegateCommand LoginCommand => new (async () => await LoginAsync());

        #endregion

        #region 屬性
        public string? UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }
        public string? Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }
        public string Database
        {
            get => _database;
            set => SetProperty(ref _database, value);
        }
        #endregion

        #region 登錄邏輯
        private async Task LoginAsync()
        {
            bool success = await _dataService.InitializeAsync(UserName, Password, Database);
            if (success)
            {
                var loginInfo = await _dataService.GetLoginInfoAsync();
                var paras=new DialogParameters();
                paras.Add("LoginInfo", loginInfo);
                RequestClose.Invoke(paras,ButtonResult.OK);
            }
            else
            {

            }
        }
        #endregion
    }
}
