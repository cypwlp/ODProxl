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
            set => SetProperty(ref _loginInfo, value);
        }
        #endregion

        #region 菜單與導航邏輯

        private async Task LoadMenuAsync()
        {
            MenuItems =
            [
                new() { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "HomePage" },
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

        /// <summary>
        /// 【唯一保留的 NavigateAsync】只接受 string
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

        /// <summary>
        /// 預設導航到首頁（已改呼叫 string 版本）
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