using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
}