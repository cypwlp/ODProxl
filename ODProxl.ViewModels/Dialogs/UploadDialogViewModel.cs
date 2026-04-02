using ODProxl.Services;

namespace ODProxl.ViewModels.Dialogs
{
    public class UploadDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IUpdateService _updateService;
        private readonly IDialogService _dialogService;

        public UploadDialogViewModel(IUpdateService updateService, IDialogService dialogService)
        {
            _updateService = updateService;
            _dialogService = dialogService;

            AvailableRids = new List<string>
            {
                "所有平台 (All Platforms)",
                "win-x64", "win-arm64",
                "osx-x64", "osx-arm64",
                "linux-x64", "linux-arm64"
            };
            SelectedRid = "所有平台 (All Platforms)";
            Version = "v1.0.1";

            ConfirmCommand = new DelegateCommand(async () => await ExecuteConfirmAsync(), CanExecuteConfirm)
                .ObservesProperty(() => Version)
                .ObservesProperty(() => SelectedRid);

            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        public List<string> AvailableRids { get; }
        private string _selectedRid = string.Empty;
        public string SelectedRid
        {
            get => _selectedRid;
            set => SetProperty(ref _selectedRid, value);
        }

        private string _version = "v1.0.1";
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        private string _dllFiles = string.Empty;
        public string DllFiles
        {
            get => _dllFiles;
            set => SetProperty(ref _dllFiles, value);
        }

        private string _updateDescription = string.Empty;
        public string UpdateDescription
        {
            get => _updateDescription;
            set => SetProperty(ref _updateDescription, value);
        }

        private string _codeDescription = string.Empty;
        public string CodeDescription
        {
            get => _codeDescription;
            set => SetProperty(ref _codeDescription, value);
        }

        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DialogCloseListener RequestClose { get; set; } = default!;

        private bool CanExecuteConfirm()
            => !string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(SelectedRid);

        // ==================== 【核心修改】同時執行 DLL + Velopack 發布 ====================
        private async Task ExecuteConfirmAsync()
        {
            try
            {
                Console.WriteLine("[UploadDialog] 開始同時發布 DLL + Velopack...");

                // 1. Velopack 發布（git commit + tag + push）
                bool velopackSuccess = await _updateService.PublishVelopackVersionAsync(
                    Version, UpdateDescription, CodeDescription);

                // 2. DLL 發布（可多平台）
                bool dllAllSuccess = true;

                if (SelectedRid == "所有平台 (All Platforms)")
                {
                    var platforms = AvailableRids.Where(r => r != "所有平台 (All Platforms)").ToList();
                    Console.WriteLine($"[UploadDialog] 同時發布到 {platforms.Count} 個 DLL 平台...");

                    foreach (var rid in platforms)
                    {
                        Console.WriteLine($"[UploadDialog] → 上傳平台: {rid}");
                        bool success = await _updateService.PublishNewDllVersionAsync(
                            Version, DllFiles, UpdateDescription, CodeDescription, rid);
                        if (!success)
                        {
                            Console.WriteLine($"[UploadDialog] ❌ {rid} 上傳失敗！");
                            dllAllSuccess = false;
                        }
                    }
                }
                else
                {
                    bool success = await _updateService.PublishNewDllVersionAsync(
                        Version, DllFiles, UpdateDescription, CodeDescription, SelectedRid);
                    if (!success) dllAllSuccess = false;
                }

                // 最終結果
                if (velopackSuccess && dllAllSuccess)
                    Console.WriteLine($"[UploadDialog] 🎉 全部發布成功！版本 {Version}");
                else if (velopackSuccess)
                    Console.WriteLine($"[UploadDialog] ⚠️ Velopack 成功，但部分 DLL 發布失敗");
                else if (dllAllSuccess)
                    Console.WriteLine($"[UploadDialog] ⚠️ DLL 成功，但 Velopack Git 發布失敗");
                else
                    Console.WriteLine($"[UploadDialog] ❌ 兩種發布均失敗");

                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UploadDialog] 嚴重錯誤：{ex.Message}");
                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            }
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}