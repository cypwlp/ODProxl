using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Material.Icons;
using Material.Icons.Avalonia;

namespace ODProxl.Dialogs;

public partial class LoginDialog : UserControl
{
    public LoginDialog()
    {
        InitializeComponent();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (VisualRoot is Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.SystemDecorations = SystemDecorations.None;
        }
    }

    /// <summary>
    /// 最小化按鈕
    /// </summary>
    private void BtnMin_Click(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is Window window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    /// <summary>
    /// 最大化/還原按鈕
    /// </summary>
    private void BtnMax_Click(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    /// <summary>
    /// 關閉按鈕
    /// </summary>
    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is Window window)
        {
            window.Close();
        }
    }

    /// <summary>
    /// 切換窗口最大化狀態並更新圖標
    /// </summary>
    private void ToggleMaximize()
    {
        if (VisualRoot is Window window)
        {


            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
                if (this.FindControl<MaterialIcon>("MaxIcon") is MaterialIcon icon)
                    icon.Kind = MaterialIconKind.WindowMaximize;
            }
            else
            {
                window.WindowState = WindowState.Maximized;
                if (this.FindControl<MaterialIcon>("MaxIcon") is MaterialIcon icon)
                    icon.Kind = MaterialIconKind.WindowRestore;
            }
        }
    }
}