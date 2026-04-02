using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Material.Icons;
using ODProxl.ViewModels.Dialogs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ODProxl
{
    public partial class UploadDialog : UserControl
    {
        public UploadDialog()
        {
            InitializeComponent();
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is Window window)
            {
                window.SystemDecorations = SystemDecorations.None;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.CanResize = false;
            }
        }
        #region 標題欄拖動
        private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (VisualRoot is Window window)
                {
                    window.BeginMoveDrag(e);
                }
            }
        }
        #endregion

        #region 自訂視窗控制按鈕
        private void BtnMin_Click(object? sender, RoutedEventArgs e)
        {
            if (VisualRoot is Window window)
                window.WindowState = WindowState.Minimized;
        }

        private void BtnMax_Click(object? sender, RoutedEventArgs e)
        {
            if (VisualRoot is Window window)
            {
                if (window.WindowState == WindowState.Maximized)
                    window.WindowState = WindowState.Normal;
                else
                    window.WindowState = WindowState.Maximized;

                // 更新最大化/還原圖示
                if (MaxIcon != null)
                {
                    MaxIcon.Kind = window.WindowState == WindowState.Maximized
                        ? MaterialIconKind.WindowRestore
                        : MaterialIconKind.WindowMaximize;
                }
            }
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            if (VisualRoot is Window window)
                window.Close();
        }
        #endregion

        #region 選擇 DLL 檔案
        private async void SelectDllFiles_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as UploadDialogViewModel;
            if (vm == null)
            {
                Console.WriteLine("[UploadDialog] ViewModel 未正確綁定！");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            try
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "選擇 DLL 檔案或清單文件",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("DLL / JSON Files")
                        {
                            Patterns = new[] { "*.dll", "*.json" }
                        }
                    }
                });

                if (files.Count > 0)
                {
                    var paths = files.Select(f => f.Path.LocalPath).ToList();
                    vm.DllFiles = string.Join(",", paths);
                    Console.WriteLine($"[UploadDialog] 已選擇 {paths.Count} 個檔案");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UploadDialog] 檔案選擇失敗：{ex.Message}");
            }
        }
        #endregion
    }
}