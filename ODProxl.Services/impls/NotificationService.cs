using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services.impls
{
    public class NotificationService : INotificationService
    {
        public void Show(Notification notification)
        {
            // 确保在 UI 线程执行
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var manager = GetCurrentNotificationManager();
              manager?.Show(notification);
            });
        }

        public void Show(string title, string message, NotificationType type = NotificationType.Information, TimeSpan? expiration = null)
        {
            var notification = new Notification(
        title,
        message,
        type,
        expiration ?? TimeSpan.FromSeconds(4));
            Show(notification);
        }

        private WindowNotificationManager? GetCurrentNotificationManager()
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var activeWindow = lifetime?.Windows.FirstOrDefault(w => w.IsActive);
            if (activeWindow == null)
                return null;
            return activeWindow.GetVisualDescendants().OfType<WindowNotificationManager>().FirstOrDefault();
        }
    }
}
