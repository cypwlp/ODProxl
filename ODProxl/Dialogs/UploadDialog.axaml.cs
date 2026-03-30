using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ODProxl.ViewModels.Dialogs;
using System.Linq;

namespace ODProxl;

public partial class UploadDialog : UserControl
{
    public UploadDialog()
    {
        InitializeComponent();
    }
    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // 如果这个 UserControl 是放在 Window 里的，通过 VisualRoot 获取 Window 并拖动
            if (VisualRoot is Window window)
            {
                window.BeginMoveDrag(e);
            }
        }
    }

    private async void SelectDllFiles_Click(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as UploadDialogViewModel;
        if (vm == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "選擇 DLL 檔案或清單文件",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("DLL / JSON")
                {
                    Patterns = new[] { "*.dll", "*.json" }
                }
            }
        });

        if (files.Count > 0)
        {
            // 自動把選取的完整路徑用逗號串接，填入 TextBox
            var paths = files.Select(f => f.Path.LocalPath).ToList();
            vm.DllFiles = string.Join(", ", paths);
        }
    }
}