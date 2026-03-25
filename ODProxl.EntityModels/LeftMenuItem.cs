using Material.Icons;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows.Input;   // 新增

namespace ODProxl.EntityModels
{
    public class LeftMenuItem : BindableBase
    {
        private MaterialIconKind _icon;
        private string? _title;
        private string? _viewName;
        private ObservableCollection<string>? limitUserName;
        private string? commandName;
        private ICommand? _command;   // 新增

        public ObservableCollection<LeftMenuItem> SubItems { get; set; } = new();

        public bool HasSubItems => SubItems != null && SubItems.Count > 0;
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

        // 新增：菜单项的命令
        public ICommand? Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }
    }
}