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

        public UploadDialogViewModel(IUpdateService updateService)
        {
            _updateService = updateService;

            // 支持一键全平台 + 单个平台
            AvailableRids = new List<string>
            {
                "所有平台 (All Platforms)",
                "win-x64", "win-arm64",
                "osx-x64", "osx-arm64",
                "linux-x64", "linux-arm64"
            };
            SelectedRid = "所有平台 (All Platforms)";   // 默认就是全平台

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
                if (SelectedRid == "所有平台 (All Platforms)")
                {
                    var platforms = AvailableRids.Where(r => r != "所有平台 (All Platforms)").ToList();
                    Console.WriteLine($"[UploadDialog] 开始同时发布到 {platforms.Count} 个平台...");

                    foreach (var rid in platforms)
                    {
                        Console.WriteLine($"[UploadDialog] → 上传平台: {rid}");
                        await _updateService.PublishNewDllVersionAsync(
                            Version, DllFiles, UpdateDescription, CodeDescription, rid);
                    }

                    Console.WriteLine($"[UploadDialog] ✅ 所有平台发布完成！");
                    RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                }
                else
                {
                    bool success = await _updateService.PublishNewDllVersionAsync(
                        Version, DllFiles, UpdateDescription, CodeDescription, SelectedRid);

                    if (success)
                        RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UploadDialog] 发布失败：{ex.Message}");
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