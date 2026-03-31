using Avalonia.Markup.Xaml;
using Avalonia.Controls;

namespace ODProxl.Dialogs
{
    public partial class InputDialog : UserControl
    {
        public InputDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}