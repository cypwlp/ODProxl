using Avalonia.Controls;
using ODProxl.EntityModels;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Material.Icons;
namespace ODProxl.ViewModels
{
    public class MainWinViewModel:BindableBase
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
            set => SetProperty(ref _loginInfo, value);
        }
        #endregion

        #region 菜單於導航邏輯

        private async Task LoadMenuAsync()
        {
            MenuItems =
            [
                new() { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },
                new()
                {
                    Icon = MaterialIconKind.Database,
                    Title = "檢測系統",
                    SubItems =
                    [
                        new() { Icon = MaterialIconKind.Magnify, Title = "實時檢測", ViewName = "Detect" },
                        new() { Icon = MaterialIconKind.History, Title = "歷史數據", ViewName = "History" },
                        new() { Icon = MaterialIconKind.SmokeDetector, Title = "自動檢測", ViewName = "AutoDet" },
                        new() { Icon = MaterialIconKind.GlobeModel, Title = "模型管理", ViewName = "OnnxModelMS" }

                    ]
                },

                new LeftMenuItem
                {
                    Icon = MaterialIconKind.ChatProcessing,
                    Title = "流程管理",
                    SubItems =
                    [
                        new() { Icon = MaterialIconKind.FileTree, Title = "當前流程", ViewName = "Process" },
                        new() { Icon = MaterialIconKind.FormatListBulleted, Title = "工單列表", ViewName = "WorkOrder" }
                    ]
                },
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.CogOutline, Title = "設置",
                    SubItems =
                    [
                        new() { Icon = MaterialIconKind.Cog, Title = "檢測設置", ViewName = "Settings" },
                        new() { Icon = MaterialIconKind.Account, Title = "個人中心", ViewName = "Personal" }
                    ]
                }
            ];
        }

        public async Task NavigateAsync(LeftMenuItem menuItem, NavigationParameters? paras = null)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName)) return;

            var parameters = new NavigationParameters
            {
                { "LoginInfo", LoginInfo }
            };

            if (paras != null)
            {
                foreach (var param in paras)
                {
                    if (!parameters.ContainsKey(param.Key))
                        parameters.Add(param.Key, param.Value);
                }
            }


            _regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem.ViewName,
                callback =>
                {
                    if (callback.Success == true)
                    {
                        _journal = callback.Context.NavigationService.Journal;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"導航至 {menuItem.ViewName} 失敗: {callback.Exception?.Message}");
                    }
                },
                parameters
            );
        }
        public async Task NavigateAsync(string menuItem, NavigationParameters? paras = null)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem)) return;

            var parameters = new NavigationParameters
            {
                { "LoginInfo", LoginInfo }
            };

            if (paras != null)
            {
                foreach (var param in paras)
                {
                    if (!parameters.ContainsKey(param.Key))
                        parameters.Add(param.Key, param.Value);
                }
            }
            _regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem,
                callback =>
                {
                    if (callback.Success == true)
                    {
                        _journal = callback.Context.NavigationService.Journal;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"導航至 {menuItem} 失敗: {callback.Exception?.Message}");
                    }
                },
                parameters
            );
        }
        private async Task BackAsync()
        {
            if (_journal?.CanGoBack == true)
            {
                _journal.GoBack();
            }
        }

        private async Task ForwardAsync()
        {
            if (_journal?.CanGoForward == true)
            {
                _journal.GoForward();
            }
        }


        public async Task DefaultNavigateAsync()
        {
            var homeItem = MenuItems.FirstOrDefault(x => x.ViewName == "Home");
            if (homeItem != null)
            {
                await NavigateAsync(homeItem, null);
            }
        }
        #endregion



    }
}  
