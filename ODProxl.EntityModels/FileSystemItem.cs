using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class FileSystemItem : BindableBase
    {
        public ObservableCollection<FileSystemItem> Items { get; } = [];
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

                if (value && OwnerViewModel != null)
                {
                    // 互斥：關閉其他項目
                    foreach (var other in Items)
                    {
                        if (other != this)
                        {
                            other.IsEnabled = false;
                        }
                    }
                }
            }
        }

        public string? OwnerViewModel { get; set; }
    }
}
