using Prism.Commands;
using Prism.Mvvm;


namespace ODProxl.ViewModels.Dialogs
{
    public class InputDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "新增全域類別";
        private string _message = "請輸入新類別名稱";
        private string _defaultText = "";
        private string _result = "";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string DefaultText
        {
            get => _defaultText;
            set => SetProperty(ref _defaultText, value);
        }

        public string Result
        {
            get => _result;
            private set => SetProperty(ref _result, value);
        }

        public DelegateCommand OkCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public InputDialogViewModel()
        {
            OkCommand = new DelegateCommand(ExecuteOk);
            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        private void ExecuteOk()
        {
            Result = DefaultText?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(Result))
            {
                RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
                return;
            }

            var parameters = new DialogParameters();
            parameters.Add("Result", Result);

            // 正确创建带参数的 DialogResult
            var result = new DialogResult(ButtonResult.OK);
            result.Parameters = parameters;
            RequestClose.Invoke(result);
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #region IDialogAware 实现

        // 对话框标题（用于 UI 显示）
        public string TitleForDialog { get; set; } = "新增全域類別";

        // 由 Prism 框架注入，不需要手动初始化
        public DialogCloseListener RequestClose { get; set; }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Title = parameters.GetValue<string>("Title") ?? "新增全域類別";
            Message = parameters.GetValue<string>("Message") ?? "請輸入新類別名稱";
            DefaultText = parameters.GetValue<string>("DefaultText") ?? "";
        }

        #endregion
    }
}