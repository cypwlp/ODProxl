using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Prism.DryIoc;
using Prism.Ioc;

namespace ODProxl
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell() => null!;


        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
 
        }
    }
}