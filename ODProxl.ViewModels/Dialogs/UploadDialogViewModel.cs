using ODProxl.Services;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Dialogs
{
    public class UploadDialogViewModel : BindableBase, IDialogAware
    {
        private string _version = string.Empty;
        public string Version { get => _version; set => SetProperty(ref _version, value); }

        private string _gitRepoPath = Environment.CurrentDirectory;
        public string GitRepoPath { get => _gitRepoPath; set => SetProperty(ref _gitRepoPath, value); }

        private string _extensionFile = string.Empty;
        public string ExtensionFile { get => _extensionFile; set => SetProperty(ref _extensionFile, value); }

        private string _updateDescription = string.Empty;
        public string UpdateDescription { get => _updateDescription; set => SetProperty(ref _updateDescription, value); }

        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private readonly IUploadService _uploadService;

        public UploadDialogViewModel(IUploadService uploadService)
        {
            _uploadService = uploadService;
            UpdateCommand = new DelegateCommand(async () => await ExecuteUploadAsync());
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        private async Task ExecuteUploadAsync()
        {
            if (string.IsNullOrWhiteSpace(Version) || string.IsNullOrWhiteSpace(ExtensionFile))
                return; // 可自行加入 MessageBox 提示

            try
            {
                // 1. 上傳到中國伺服器
                await _uploadService.PushUploadAsync(Version, UpdateDescription, ExtensionFile);

                // 2. 自動 git tag + push（觸發 Velopack 建置）
                await _uploadService.CreateAndPushGitTagAsync(Version, UpdateDescription, GitRepoPath);

                RequestClose.Invoke(ButtonResult.OK);
            }
            catch (Exception ex)
            {
                // TODO: 顯示錯誤提示（可使用 MessageBox）
                Console.WriteLine($"上傳失敗：{ex.Message}");
            }
        }

        #region IDialogAware
        public DialogCloseListener RequestClose { get; set; }
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
        #endregion
    }
}