using Avalonia;
using System;
using Velopack;

namespace ODProxl
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // 【重要】這行必須放在 Main 的第一行
            // 它負責處理安裝、卸載、更新啟動等底層邏輯
            // 如果這行執行了（例如在安裝過程中），它會結束進程，不會進入後面的 Avalonia 啟動
            VelopackApp.Build().Run();

            // 正常的 Avalonia 啟動邏輯
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
