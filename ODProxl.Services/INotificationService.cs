using Avalonia.Controls.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface INotificationService
    {
        void Show(Notification notification);
        void Show(string title, string message, NotificationType type = NotificationType.Information, TimeSpan? expiration = null);
    }
}
