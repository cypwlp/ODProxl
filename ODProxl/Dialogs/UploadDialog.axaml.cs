//using Avalonia;
//using Avalonia.Controls;
//using Avalonia.Interactivity;
//using Avalonia.Markup.Xaml;
//using Avalonia.Platform.Storage;
//using ODProxl.ViewModels.Dialogs;
//using System.Linq;

//namespace ODProxl;

//public partial class UploadDialog : UserControl
//{
//    public UploadDialog()
//    {
//        InitializeComponent();
//    }
//    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
//    {
//        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
//        {
//            // 如果这个 UserControl 是放在 Window 里的，通过 VisualRoot 获取 Window 并拖动
//            if (VisualRoot is Window window)
//            {
//                window.BeginMoveDrag(e);
//            }
//        }
//    }

//    private void SelectDllFiles_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
//    {
//        // TODO: 實作檔案選擇器
//        var dialog = new OpenFileDialog
//        {
//            AllowMultiple = true,
//            Filters = { new FileDialogFilter { Name = "DLL Files", Extensions = { "dll", "json" } } }
//        };

//        var window = (Window)this.VisualRoot!;
//        var files = dialog.ShowAsync(window).Result;
//        if (files?.Any() == true)
//        {
//            var vm = (UploadDialogViewModel)this.DataContext!;
//            vm.DllFiles = string.Join(",", files);
//        }
//    }

//    //private async void SelectDllFiles_Click(object sender, RoutedEventArgs e)
//    //{
//    //    var vm = DataContext as UploadDialogViewModel;
//    //    if (vm == null) return;

//    //    var topLevel = TopLevel.GetTopLevel(this);
//    //    if (topLevel == null) return;

//    //    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
//    //    {
//    //        Title = "選擇 DLL 檔案或清單文件",
//    //        AllowMultiple = true,
//    //        FileTypeFilter = new[]
//    //        {
//    //            new FilePickerFileType("DLL / JSON")
//    //            {
//    //                Patterns = new[] { "*.dll", "*.json" }
//    //            }
//    //        }
//    //    });

//    //    if (files.Count > 0)
//    //    {
//    //        // 自動把選取的完整路徑用逗號串接，填入 TextBox
//    //        var paths = files.Select(f => f.Path.LocalPath).ToList();
//    //        vm.DllFiles = string.Join(", ", paths);
//    //    }
//    //}
//}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        // 標題欄拖動
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

        // 選擇檔案按鈕（已改用現代非阻塞方式）
        private async void SelectDllFiles_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as UploadDialogViewModel;
            if (vm == null)
            {
                Console.WriteLine("[UploadDialog] ViewModel 未正確綁定！");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                Console.WriteLine("[UploadDialog] 無法取得 TopLevel");
                return;
            }

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
    }
}