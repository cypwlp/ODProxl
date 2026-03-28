using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Material.Icons;
using Material.Icons.Avalonia;
using ODProxl.ViewModels.Dialogs; // 确保引用了 ViewModel 命名空间

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
            window.SystemDecorations = SystemDecorations.None;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.CanResize = false;
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
    /// 用户名输入框按 Enter 键时跳转到密码框
    /// </summary>
    private void UserNameTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PasswordTextBox?.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 密码框按 Enter 键时触发登录命令
    /// </summary>
    private void PasswordTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is LoginDialogViewModel vm)
            {
                vm.LoginCommand?.Execute();
            }
            e.Handled = true;
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