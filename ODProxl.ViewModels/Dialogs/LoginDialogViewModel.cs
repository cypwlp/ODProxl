using Avalonia.Logging;
using ODProxl.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Dialogs
{
    public class LoginDialogViewModel : BindableBase, IDialogAware
    {
        #region IDialogAware Implementation
        public string Title => "登入系統";
        public DialogCloseListener RequestClose { get; private set; }
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
        #endregion

        #region 字段
        private readonly IDialogService? _dialogService;
        private readonly IDataService? _dataService;
        private RemoteService.LoginInfo? logInfo;
        private string? _userName;
        private string? _password;
        private string _database = "TopmixData";
        public DelegateCommand LoginCommand => new(async () => await LoginAsync());
        public LoginDialogViewModel(IDataService dataService, IDialogService dialogService)
        {
            _dataService = dataService;
            _dialogService = dialogService;
        }

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
            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
            {
                return;
            }

            // 只呼叫一次 InitializeAsync（使用使用者輸入的帳密 + 應用程式主要資料庫 "DetOB"）
            bool success = await _dataService!.InitializeAsync(UserName!, Password!, "DetOB");

            if (success)
            {
                var loginInfo = await _dataService.GetLoginInfoAsync();

                // 把登入資訊傳給下一頁
                var paras = new DialogParameters();
                paras.Add("LoginInfo", loginInfo);

                RequestClose.Invoke(paras, ButtonResult.OK);
            }
            else
            {
                // 登入失敗處理（可自行加上訊息框）

            }
        }
        #endregion 
    }
}