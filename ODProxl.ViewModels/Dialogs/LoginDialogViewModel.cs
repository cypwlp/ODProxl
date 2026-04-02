using Avalonia.Threading;
using Material.Styles.Controls;
using Material.Styles.Models;
using ODProxl.Services;
using Prism.Commands;
using System;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Dialogs
{
    public class LoginDialogViewModel : BindableBase, IDialogAware
    {
        #region IDialogAware Implementation
        public string Title => "登入系統";
        public DialogCloseListener RequestClose { get; private set; } = new();
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
        #endregion

        #region 字段
        private readonly IDataService? _dataService;
        private readonly IDialogService? _dialogService;
        private readonly INotificationService? _notificationService;   // 如果你還想保留，可以繼續用

        private string? _userName;
        private string? _password;
        private string _database = "TopmixData";
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

        public DelegateCommand LoginCommand { get; }

        public LoginDialogViewModel(IDataService dataService, IDialogService dialogService, INotificationService? notificationService = null)
        {
            _dataService = dataService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            LoginCommand = new DelegateCommand(async () => await LoginAsync());
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
                return;
        
            bool success = await _dataService!.InitializeAsync(UserName!, Password!, "ODProxl");
            await Task.Delay(200);
            try
            {
                if (success==true)
                {
                    var loginInfo = await _dataService.GetLoginInfoAsync();
                    var paras = new DialogParameters { { "LoginInfo", loginInfo } };
                    RequestClose.Invoke(paras, ButtonResult.OK);
                }
                else
                {
                    string content = "用户名或密码错误，请重试。";
                    SnackbarModel snackbar = new SnackbarModel(content, TimeSpan.FromSeconds(5));
                    SnackbarHost.Post(snackbar, "LoginSnackbarHost", DispatcherPriority.Normal);
                }
            }
            catch (Exception ex)
            {
                string content = $"登入過程中發生錯誤: {ex.Message}";
                SnackbarModel snackbar = new SnackbarModel(content, TimeSpan.FromSeconds(5));
                SnackbarHost.Post(snackbar, "LoginSnackbarHost", DispatcherPriority.Normal);
            }
        }
    }
}