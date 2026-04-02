using ODProxl.Services;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        private async Task ExecuteConfirmAsync()
        {
            try
            {
                Console.WriteLine("[UploadDialog] 開始上傳...");

                bool allSuccess = true;

                if (SelectedRid == "所有平台")
                {
                    var platforms = AvailableRids.Where(r => r != "所有平台 (All Platforms)").ToList();
                    Console.WriteLine($"[UploadDialog] 開始同時發布到 {platforms.Count} 個平台...");

                    foreach (var rid in platforms)
                    {
                        Console.WriteLine($"[UploadDialog] → 上傳平台: {rid}");
                        bool success = await _updateService.PublishNewDllVersionAsync(
                            Version, DllFiles, UpdateDescription, CodeDescription, rid);

                        if (!success)
                        {
                            Console.WriteLine($"[UploadDialog] ❌ {rid} 上傳失敗！");
                            allSuccess = false;
                        }
                    }

                    if (allSuccess)
                        Console.WriteLine($"[UploadDialog] ✅ 所有平台發布完成！");
                    else
                        Console.WriteLine($"[UploadDialog] ⚠️ 部分平台發布失敗，請查看上方詳細錯誤！");
                }
                else
                {
                    bool success = await _updateService.PublishNewDllVersionAsync(
                        Version, DllFiles, UpdateDescription, CodeDescription, SelectedRid);

                    if (success)
                        Console.WriteLine($"[UploadDialog] ✅ 版本 {Version} 已成功發布到 {SelectedRid}！");
                    else
                        Console.WriteLine($"[UploadDialog] ❌ {SelectedRid} 上傳失敗，請查看上方錯誤詳情！");
                }

                // 直接關閉對話框，不再彈成功/失敗視窗
                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UploadDialog] 嚴重錯誤：{ex.Message}");
                RequestClose.Invoke(new DialogResult(ButtonResult.OK));  // 還是讓對話框能關閉
            }
        }
        private async Task ShowMessageAsync(string title, string message, ButtonResult defaultResult)
        {
            var parameters = new DialogParameters
            {
                { "Title", title },
                { "Message", message }
            };
            //await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
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