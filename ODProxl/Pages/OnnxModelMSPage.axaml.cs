using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace ODProxl;

public partial class OnnxModelMSPage : UserControl
{
    public OnnxModelMSPage()
    {
        InitializeComponent();
    }
    private void Search_OnKeyDown(object? sender, KeyEventArgs e)
    {
        var textBox = (TextBox)sender!;
        if (e.Key == Key.Enter)
            this.Get<Button>("SearchButton").Command!.Execute(textBox.Text);
    }

    private void TextBox_OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        var textBox = (TextBox)sender!;
        Dispatcher.UIThread.Post(textBox.SelectAll);
    }
}