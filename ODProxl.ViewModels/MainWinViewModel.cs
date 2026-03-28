using Avalonia.Controls;
using Material.Icons;
using Material.Icons.Avalonia;   // ← 新增這一行
using ODProxl.EntityModels;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ODProxl.ViewModels
{
    public class MainWinViewModel : BindableBase
    {
        #region 字段
        private readonly IRegionManager? _regionManager;
        private IRegionNavigationJournal? _journal;
        private LoginInfo? _loginInfo;
        private bool _isMenuExpanded;
        private LeftMenuItem? _selectedMenuItem;
        private LeftMenuItem? _selectedFlyoutItem;
        #endregion

        #region 屬性
        public ObservableCollection<LeftMenuItem>? MenuItems { get; set; }
        public ObservableCollection<LeftMenuItem>? MenuFlyoutItems { get; set; }   // 內部用來過濾權限
        public ObservableCollection<MenuItem> FlyoutMenuItems { get; } = new();     // ← 給 MenuFlyout 真正使用的集合

        public LoginInfo? LoginInfo
        {
            get => _loginInfo;
            set
            {
                if (SetProperty(ref _loginInfo, value))
                {
                    if (MenuItems != null)
                        ApplyPermissionFilter();
                    if (MenuFlyoutItems != null)
                        FilterFlyoutItems();
                }
            }
        }

        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set => SetProperty(ref _isMenuExpanded, value);
        }

        public LeftMenuItem? SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value))
                {
                    if (value != null && !string.IsNullOrEmpty(value.ViewName))
                    {
                        _ = NavigateAsync(value.ViewName);
                    }
                }
            }
        }

        public LeftMenuItem? SelectedFlyoutItem
        {
            get => _selectedFlyoutItem;
            set => SetProperty(ref _selectedFlyoutItem, value);
        }
        #endregion

        #region 命令
        public DelegateCommand BackCommand { get; }
        public DelegateCommand ForwardCommand { get; }
        #endregion

        #region 建構函式
        public MainWinViewModel(IRegionManager? regionManager)
        {
            _regionManager = regionManager;
            BackCommand = new DelegateCommand(OnBack);
            ForwardCommand = new DelegateCommand(OnForward);

            LoadMenu();
            LoadMenuFlyout();
        }
        #endregion

        #region 菜單構建與權限過濾
        private void LoadMenuFlyout()
        {
            // 1. 先建立原始 LeftMenuItem（用來做權限過濾）
            MenuFlyoutItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Account, Title = "個性設置", ViewName = "UserPreferencePage", LimitUserName = ["AllUser"], Command = new DelegateCommand(async () => await NavigateAsync("UserPreferencePage",new NavigationParameters{ { "LoginInfo",LoginInfo} })) },
                new LeftMenuItem { Icon = MaterialIconKind.Cog, Title = "檢測设置", ViewName = "Settings", LimitUserName = ["AllUser"], Command = new DelegateCommand(async () => await NavigateAsync("Settings")) },
                new LeftMenuItem { Icon = MaterialIconKind.Information, Title = "關於", ViewName = "AboutDialog", LimitUserName = ["AllUser"], Command = new DelegateCommand(async () => await ShowDialogAsync("AboutDialog")) },
                new LeftMenuItem { Icon = MaterialIconKind.LogoutVariant, Title = "退出登录", ViewName = null, LimitUserName = ["AllUser"], Command = new DelegateCommand(OnLogout) },
                new LeftMenuItem { Icon = MaterialIconKind.CodeGreaterThanOrEqual, Title = "程序信息", ViewName = "UploadDialog", LimitUserName = ["L5940","L5126","1817"], Command = new DelegateCommand(async () => await ShowDialogAsync("UploadDialog", new DialogParameters { { "LoginInfo", LoginInfo } })) }
            };

            RebuildFlyoutMenuItems();   // 建立真正的 MenuItem 集合
        }

        private void RebuildFlyoutMenuItems()
        {
            FlyoutMenuItems.Clear();
            if (MenuFlyoutItems == null) return;

            foreach (var item in MenuFlyoutItems)
            {
                var menuItem = new MenuItem
                {
                    Header = item.Title,
                    Command = item.Command,
                    Icon = new MaterialIcon { Kind = item.Icon, Width = 20, Height = 20 }
                };
                FlyoutMenuItems.Add(menuItem);
            }
        }

        private void LoadMenu()
        {
            // 你的左側 TreeView 菜單保持不變
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "HomePage", LimitUserName = ["AllUser"] },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.Database,
                    Title = "檢測系統",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.Magnify, Title = "实时檢測", ViewName = "Detect" },
                        new LeftMenuItem { Icon = MaterialIconKind.History, Title = "歷史數據", ViewName = "History" },
                        new LeftMenuItem { Icon = MaterialIconKind.SmokeDetector, Title = "自動檢測", ViewName = "AutoDet" },
                        new LeftMenuItem { Icon = MaterialIconKind.GlobeModel, Title = "模型管理", ViewName = "OnnxModelMSPage", LimitUserName = new ObservableCollection<string> { "L5940", "L5126" } }
                    }
                },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.ChatProcessing,
                    Title = "流程管理",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.FileTree, Title = "當前流程", ViewName = "Process" },
                        new LeftMenuItem { Icon = MaterialIconKind.FormatListBulleted, Title = "工單列表", ViewName = "WorkOrder" }
                    }
                },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.CogOutline,
                    Title = "設置",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.Cog, Title = "檢測設置", ViewName = "Settings" },
                        new LeftMenuItem { Icon = MaterialIconKind.Account, Title = "個人中心", ViewName = "Personal" }
                    }
                }
            };

            if (LoginInfo != null)
                ApplyPermissionFilter();
        }

        private void ApplyPermissionFilter()
        { /* 你的原程式碼不變 */
            if (MenuItems == null) return;
            string userName = LoginInfo?.LoginName ?? "";
            var filtered = new ObservableCollection<LeftMenuItem>();
            foreach (var item in MenuItems)
            {
                var filteredItem = FilterMenuItem(item, userName);
                if (filteredItem != null)
                    filtered.Add(filteredItem);
            }
            MenuItems = filtered;
            RaisePropertyChanged(nameof(MenuItems));
        }

        private LeftMenuItem? FilterMenuItem(LeftMenuItem item, string userName)
        { /* 你的原程式碼不變 */
            if (item.SubItems != null && item.SubItems.Any())
            {
                var filteredSubs = new ObservableCollection<LeftMenuItem>();
                foreach (var sub in item.SubItems)
                {
                    var filteredSub = FilterMenuItem(sub, userName);
                    if (filteredSub != null)
                        filteredSubs.Add(filteredSub);
                }
                if (!filteredSubs.Any())
                {
                    if (!HasPermission(item, userName))
                        return null;
                    item.SubItems = filteredSubs;
                }
                else
                {
                    item.SubItems = filteredSubs;
                }
            }
            if (!HasPermission(item, userName))
                return null;
            return item;
        }

        private bool HasPermission(LeftMenuItem item, string userName)
        {
            if (item.LimitUserName == null || item.LimitUserName.Count == 0)
                return true;
            if (item.LimitUserName.Contains("AllUser"))
                return true;
            return item.LimitUserName.Contains(userName);
        }

        private void FilterFlyoutItems()
        {
            if (MenuFlyoutItems == null) return;
            string userName = LoginInfo?.LoginName ?? "";

            // 過濾後直接重建 FlyoutMenuItems
            var filtered = MenuFlyoutItems.Where(item => HasPermission(item, userName)).ToList();

            FlyoutMenuItems.Clear();
            foreach (var item in filtered)
            {
                var menuItem = new MenuItem
                {
                    Header = item.Title,
                    Command = item.Command,
                    Icon = new MaterialIcon { Kind = item.Icon, Width = 20, Height = 20 }
                };
                FlyoutMenuItems.Add(menuItem);
            }
        }
        #endregion

        #region 導航邏輯（完全不變）
        private async Task ShowDialogAsync(string dialogName, DialogParameters? paras = null)
        {
            var dialogService = Prism.Ioc.ContainerLocator.Container.Resolve<Prism.Dialogs.IDialogService>();
            await dialogService.ShowDialogAsync(dialogName, paras);
        }

        private async Task NavigateAsync(string viewName, NavigationParameters? paras = null)
        {
            if (string.IsNullOrEmpty(viewName)) return;
            var parameters = new NavigationParameters { { "LoginInfo", LoginInfo } };
            if (paras != null)
            {
                foreach (var param in paras)
                {
                    if (!parameters.ContainsKey(param.Key))
                        parameters.Add(param.Key, param.Value);
                }
            }

            _regionManager?.Regions["MainRegion"].RequestNavigate(
                viewName,
                callback =>
                {
                    if (callback.Success)
                        _journal = callback.Context.NavigationService.Journal;
                    else
                        System.Diagnostics.Debug.WriteLine($"導航至 {viewName} 失敗: {callback.Exception?.Message}");
                },
                parameters);
        }

        private void OnBack() { if (_journal?.CanGoBack == true) _journal.GoBack(); }
        private void OnForward() { if (_journal?.CanGoForward == true) _journal.GoForward(); }

        public async Task DefaultNavigateAsync()
        {
            var homeItem = MenuItems?.FirstOrDefault(x => x.ViewName == "HomePage");
            if (homeItem != null)
                await NavigateAsync(homeItem.ViewName);
        }

        private void OnLogout()
        {
            System.Diagnostics.Debug.WriteLine("退出登录");
            // 你可以在這裡加入實際登出邏輯
        }
        #endregion
    }
}