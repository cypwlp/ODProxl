using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Material.Icons;
using Material.Styles.Controls;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Input;

namespace ODProxl.ExtentControls
{
    public partial class IconActionButton : UserControl
    {
        // 原有属性
        public static readonly StyledProperty<MaterialIconKind> IconKindProperty =
            AvaloniaProperty.Register<IconActionButton, MaterialIconKind>(nameof(IconKind));

        public static readonly StyledProperty<string> ToolTipTextProperty =
            AvaloniaProperty.Register<IconActionButton, string>(nameof(ToolTipText), "提示");

        public static readonly StyledProperty<string> SnackbarMessageProperty =
            AvaloniaProperty.Register<IconActionButton, string>(nameof(SnackbarMessage), "此功能已觸發");

        public static readonly StyledProperty<NotificationType> SnackbarTypeProperty =
            AvaloniaProperty.Register<IconActionButton, NotificationType>(nameof(SnackbarType), NotificationType.Information);

        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<IconActionButton, ICommand?>(nameof(Command));

        public static readonly StyledProperty<object?> CommandParameterProperty =
            AvaloniaProperty.Register<IconActionButton, object?>(nameof(CommandParameter));

        // 新增：自定义内容属性
        public static readonly StyledProperty<object?> ContentProperty =
            AvaloniaProperty.Register<IconActionButton, object?>(nameof(Content));

        // 辅助属性：控制默认图标的可见性
        private static readonly StyledProperty<bool> IsDefaultIconVisibleProperty =
            AvaloniaProperty.Register<IconActionButton, bool>(nameof(IsDefaultIconVisible), true);

        // 辅助属性：控制自定义内容的可见性
        private static readonly StyledProperty<bool> IsCustomContentVisibleProperty =
            AvaloniaProperty.Register<IconActionButton, bool>(nameof(IsCustomContentVisible), false);

        // 路由事件
        public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
            RoutedEvent.Register<IconActionButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        // 属性访问器
        public MaterialIconKind IconKind
        {
            get => GetValue(IconKindProperty);
            set => SetValue(IconKindProperty, value);
        }

        public string ToolTipText
        {
            get => GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        public string SnackbarMessage
        {
            get => GetValue(SnackbarMessageProperty);
            set => SetValue(SnackbarMessageProperty, value);
        }

        public NotificationType SnackbarType
        {
            get => GetValue(SnackbarTypeProperty);
            set => SetValue(SnackbarTypeProperty, value);
        }

        public ICommand? Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public object? Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        // 辅助属性只读暴露给 XAML
        private bool IsDefaultIconVisible
        {
            get => GetValue(IsDefaultIconVisibleProperty);
            set => SetValue(IsDefaultIconVisibleProperty, value);
        }

        private bool IsCustomContentVisible
        {
            get => GetValue(IsCustomContentVisibleProperty);
            set => SetValue(IsCustomContentVisibleProperty, value);
        }

        public IconActionButton()
        {
            InitializeComponent();
            UpdateContentVisibility();
        }

        // 监听属性变化，当 Content 改变时更新可见性
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ContentProperty)
            {
                UpdateContentVisibility();
            }
        }

        private void UpdateContentVisibility()
        {
            bool hasCustomContent = Content != null;
            IsDefaultIconVisible = !hasCustomContent;
            IsCustomContentVisible = hasCustomContent;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var innerButton = this.FindControl<Button>("InnerButton");
            if (innerButton != null)
            {
                innerButton.Click += InnerButton_Click;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            var innerButton = this.FindControl<Button>("InnerButton");
            if (innerButton != null)
            {
                innerButton.Click -= InnerButton_Click;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void InnerButton_Click(object? sender, RoutedEventArgs e)
        {
            // 执行 MVVM Command
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }

            // 显示 Snackbar
            ShowSnackbar();

            // 触发外部 Click 事件
            RaiseEvent(new RoutedEventArgs(ClickEvent));
        }

        private void ShowSnackbar()
        {
            var snackbarHost = this.GetVisualAncestors().OfType<SnackbarHost>().FirstOrDefault();
            if (snackbarHost == null) return;

            var manager = GetNotificationManager(snackbarHost);
            if (manager != null)
            {
                var notification = new Notification
                {
                    Title = null,
                    Message = SnackbarMessage,
                    Type = SnackbarType,
                    Expiration = TimeSpan.FromSeconds(3)
                };
                manager.Show(notification);
            }
        }

        private static INotificationManager? GetNotificationManager(SnackbarHost host)
        {
            var prop = host.GetType().GetProperty("SnackbarManager", BindingFlags.Public | BindingFlags.Instance)
                        ?? host.GetType().GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(host) as INotificationManager;
        }
    }
}