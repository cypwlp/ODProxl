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
        public List<string> ModelClasses { get; set; } = new();
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
                if (value) 
                {
                    EnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler? EnabledChanged;
    }
}