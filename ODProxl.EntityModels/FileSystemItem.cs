using Prism.Mvvm;
using System;

namespace ODProxl.EntityModels
{
    public class FileSystemItem : BindableBase
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime LastModified { get; set; }

        public string CreatedTimeDisplay => LastModified.ToString("yyyy-MM-dd HH:mm");
        public string SizeDisplay => Size.HasValue
            ? $"{Size.Value / (1024.0 * 1024.0):0.##} MB"
            : "--";

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;

                _isEnabled = value;
                RaisePropertyChanged(nameof(IsEnabled));

                // 發出事件，讓 ViewModel 處理業務邏輯
                if (value) // 只有「啟用」時才觸發
                {
                    EnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // 事件：當 IsEnabled 變為 true 時觸發
        public event EventHandler? EnabledChanged;
    }
}