using Avalonia.Controls;
using Prism.Dialogs;

namespace ODProxl.Dialogs;

public partial class CustomDialogWindow : Window, IDialogWindow
{
    public IDialogResult Result { get; set; } = new DialogResult(ButtonResult.Cancel);

    public CustomDialogWindow()
    {
        InitializeComponent();
    }
}