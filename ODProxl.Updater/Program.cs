using System.Diagnostics;
using System.Text.Json;
class Program
{
    public class UpdateFile
    {
        public string Source { get; set; } = string.Empty;
        public string Dest { get; set; } = string.Empty;
    }
    public class PendingUpdate
    {
        public string Version { get; set; } = string.Empty;
        public string UpdateType { get; set; } = "Full";
        public List<UpdateFile> Files { get; set; } = new();
        public string[] RestartArgs { get; set; } = Array.Empty<string>();
        public bool DeleteAfterApply { get; set; } = true;
    }
    static async Task Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string pendingPath = Path.Combine(baseDir, "PendingUpdate", "pending.json");

        if (!File.Exists(pendingPath))
        {
            Console.WriteLine("沒有待處理更新。");
            return;
        }

        var pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(pendingPath))
            ?? throw new Exception("pending.json 格式錯誤");

        Console.WriteLine($"開始套用更新至版本 {pending.Version}...");

        // 1. 等待主程式退出
        await WaitForMainAppExitAsync("ODProxl");

        // 2. 備份（可選）
        // BackupFiles(...);

        // 3. 套用更新
        foreach (var file in pending.Files)
        {
            string src = Path.Combine(baseDir, file.Source);
            string dest = Path.Combine(baseDir, file.Dest);
            string? destDir = Path.GetDirectoryName(dest);
            if (destDir != null) Directory.CreateDirectory(destDir);

            if (File.Exists(src))
            {
                File.Copy(src, dest, true);   // 覆蓋
                Console.WriteLine($"已更新: {file.Dest}");
            }
        }

        // 4. 清理
        if (pending.DeleteAfterApply)
            TryDeleteDirectory(Path.Combine(baseDir, "PendingUpdate"));

        // 5. 重啟主程式
        string exePath = Path.Combine(baseDir, "ODProxl.exe");
        if (File.Exists(exePath))
        {
            var startInfo = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Arguments = string.Join(" ", pending.RestartArgs)
            };
            Process.Start(startInfo);
        }

        Console.WriteLine("更新完成，已重新啟動主程式。");
    }

    private static async Task WaitForMainAppExitAsync(string processName, int timeoutMs = 15000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return;

            await Task.Delay(500);
        }
        throw new TimeoutException("等待主程式退出超時");
    }
    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { /* 忽略 */ }
    }
}