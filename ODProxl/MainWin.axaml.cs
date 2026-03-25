using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;                    
using Avalonia.Interactivity;          
using Avalonia.Markup.Xaml;
using Material.Icons;                 
using Material.Icons.Avalonia;           

namespace ODProxl
{
    public partial class MainWin : Window
    {
        public MainWin()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 實現頂部欄拖拽窗口的功能
        /// </summary>
        private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    this.BeginMoveDrag(e);
                }
            }
        }

        private void btnMin_Click(object? sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnMax_Click(object? sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                if (this.FindControl<MaterialIcon>("MaxIcon") is MaterialIcon icon)
                    icon.Kind = MaterialIconKind.WindowMaximize;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                if (this.FindControl<MaterialIcon>("MaxIcon") is MaterialIcon icon)
                    icon.Kind = MaterialIconKind.WindowRestore;
            }
        }
    }
}