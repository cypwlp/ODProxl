using Material.Icons;
using Prism.Mvvm;
using System.Collections.ObjectModel;
namespace ODProxl.EntityModels
{
    public class LeftMenuItem: BindableBase
    {

        private MaterialIconKind _icon;
        private string? _title;
        private string? _viewName;
        private ObservableCollection<string>? limitUserName;
        private string? commandName;

        public ObservableCollection<LeftMenuItem> SubItems { get; set; } = new();

        // 輔助屬性：判斷是否有子項
        public bool HasSubItems => SubItems != null && SubItems.Count > 0;

        // 輔助屬性：判斷是否為導航項（有 ViewName 才能跳轉）
        public bool IsNavigationItem => !string.IsNullOrEmpty(ViewName);

        public MaterialIconKind Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string? Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string? ViewName
        {
            get => _viewName;
            set => SetProperty(ref _viewName, value);
        }
        public ObservableCollection<string>? LimitUserName
        {
            get => limitUserName;
            set => SetProperty(ref limitUserName, value);
        }

        public string? CommandName
        {
            get => commandName;
            set => SetProperty(ref commandName, value);
        }
    }
}
