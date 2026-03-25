using Avalonia.Controls;
using ODProxl.EntityModels;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Material.Icons;
using Prism.Commands;
using Prism.Mvvm;


namespace ODProxl.ViewModels
{
    public class MainWinViewModel : BindableBase
    {
        #region 字段
        private readonly IRegionManager? _regionManager;
        private IRegionNavigationJournal? _journal;
        private LoginInfo? _loginInfo;
        #endregion

        #region 属性
        public ObservableCollection<LeftMenuItem>? MenuItems { get; set; }

        public LoginInfo? LoginInfo
        {
            get => _loginInfo;
            set
            {
                if (SetProperty(ref _loginInfo, value))
                {
                    // 登录信息改变时，重新过滤菜单
                    if (MenuItems != null)
                        ApplyPermissionFilter();
                }
            }
        }
        #endregion

        #region 构造函数
        public MainWinViewModel(IRegionManager? regionManager)
        {
            _regionManager = regionManager;
            LoadMenu(); 
        }
        #endregion

        #region 菜单构建与权限过滤

        private void LoadMenu()
        {
            // 构建原始菜单（修正初始化语法）
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.Home,
                    Title = "首页",
                    ViewName = "HomePage",
                    LimitUserName = new ObservableCollection<string> { "AllUser" }
                },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.Database,
                    Title = "检测系统",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.Magnify, Title = "实时检测", ViewName = "Detect" },
                        new LeftMenuItem { Icon = MaterialIconKind.History, Title = "历史数据", ViewName = "History" },
                        new LeftMenuItem { Icon = MaterialIconKind.SmokeDetector, Title = "自动检测", ViewName = "AutoDet" },
                        new LeftMenuItem
                        {
                            Icon = MaterialIconKind.GlobeModel,
                            Title = "模型管理",
                            ViewName = "OnnxModelMS",
                            LimitUserName = new ObservableCollection<string> { "L5940", "L5126" }
                        }
                    }
                },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.ChatProcessing,
                    Title = "流程管理",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.FileTree, Title = "当前流程", ViewName = "Process" },
                        new LeftMenuItem { Icon = MaterialIconKind.FormatListBulleted, Title = "工单列表", ViewName = "WorkOrder" }
                    }
                },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.CogOutline,
                    Title = "设置",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.Cog, Title = "检测设置", ViewName = "Settings" },
                        new LeftMenuItem { Icon = MaterialIconKind.Account, Title = "个人中心", ViewName = "Personal" }
                    }
                }
            };

            // 初始权限过滤（如果有登录信息）
            if (LoginInfo != null)
                ApplyPermissionFilter();
        }

        /// <summary>
        /// 根据 LoginInfo.LoginName 过滤菜单项（递归）
        /// </summary>
        private void ApplyPermissionFilter()
        {
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

        /// <summary>
        /// 递归过滤单个菜单项
        /// </summary>
        private LeftMenuItem? FilterMenuItem(LeftMenuItem item, string userName)
        {
            // 1. 先处理子项（递归）
            if (item.SubItems != null && item.SubItems.Any())
            {
                var filteredSubs = new ObservableCollection<LeftMenuItem>();
                foreach (var sub in item.SubItems)
                {
                    var filteredSub = FilterMenuItem(sub, userName);
                    if (filteredSub != null)
                        filteredSubs.Add(filteredSub);
                }
                // 如果子项过滤后一个都没有，且该项本身没有权限（即自身没有视图或权限限制），则该项应隐藏
                if (!filteredSubs.Any())
                {
                    // 如果该项本身没有权限，且子项全隐藏，则返回 null（隐藏）
                    if (!HasPermission(item, userName))
                        return null;
                    // 如果该项本身有权限，但子项全隐藏，则保留该项，但清空子项（也可以决定保留空子项）
                    item.SubItems = filteredSubs; // 清空子项
                }
                else
                {
                    item.SubItems = filteredSubs; // 用过滤后的子项替换
                }
            }

            // 2. 判断当前项是否有权限
            if (!HasPermission(item, userName))
                return null;

            return item; // 保留该项
        }

        /// <summary>
        /// 判断一个菜单项是否允许指定用户访问
        /// </summary>
        private bool HasPermission(LeftMenuItem item, string userName)
        {
            // 如果 LimitUserName 为 null，默认所有人可访问
            if (item.LimitUserName == null || item.LimitUserName.Count == 0)
                return true;

            // 如果包含 "AllUser"，则所有人可访问
            if (item.LimitUserName.Contains("AllUser"))
                return true;

            // 检查是否包含当前用户名
            return item.LimitUserName.Contains(userName);
        }
        #endregion

        #region 导航逻辑

        /// <summary>
        /// 导航到指定视图
        /// </summary>
        public async Task NavigateAsync(string viewName, NavigationParameters? paras = null)
        {
            if (string.IsNullOrEmpty(viewName)) return;

            var parameters = new NavigationParameters
            {
                { "LoginInfo", LoginInfo }
            };

            // 合併外部傳入的參數
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
                    {
                        _journal = callback.Context.NavigationService.Journal;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"導航至 {viewName} 失敗: {callback.Exception?.Message}");
                    }
                },
                parameters
            );
        }

        /// <summary>
        /// 后退
        /// </summary>
        public async Task BackAsync()
        {
            if (_journal?.CanGoBack == true)
            {
                _journal.GoBack();
            }
        }

        /// <summary>
        /// 前进
        /// </summary>
        public async Task ForwardAsync()
        {
            if (_journal?.CanGoForward == true)
            {
                _journal.GoForward();
            }
        }

        /// <summary>
        /// 預設導航到首頁
        /// </summary>
        public async Task DefaultNavigateAsync()
        {
            var homeItem = MenuItems?.FirstOrDefault(x => x.ViewName == "HomePage");
            if (homeItem != null)
            {
                await NavigateAsync(homeItem.ViewName);
            }
        }
        #endregion
    }
}