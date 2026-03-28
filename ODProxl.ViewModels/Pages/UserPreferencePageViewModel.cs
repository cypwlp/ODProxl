using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using ODProxl.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace ODProxl.ViewModels.Pages
{
    public class UserPreferencePageViewModel : BindableBase, INavigationAware
    {
        #region INavigationAware
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoginInfo = navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
        }
        #endregion

        #region 字段
        private readonly IDataService _dataService;
        private LoginInfo? loginInfo;
        private KeyAutoMapper? _keyAutoMapper;
        #endregion

        #region 建構函式
        public UserPreferencePageViewModel(IDataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            keyAutoMapper = new KeyAutoMapper();                    // 初始化
            SaveCommandAsync = new DelegateCommand(async () => await SavePreferenceAsync());
        }
        #endregion

        #region 屬性（已補齊 XAML 所有 Binding）
        public DelegateCommand SaveCommandAsync { get; private set; }

        public LoginInfo? LoginInfo
        {
            get => loginInfo;
            set => SetProperty(ref loginInfo, value);
        }

        public KeyAutoMapper? keyAutoMapper
        {
            get => _keyAutoMapper;
            set => SetProperty(ref _keyAutoMapper, value);
        }

        public bool IsDeveloperMode => keyAutoMapper?.IsDeveloperMode ?? false;

        // XAML 通用設定區塊
        public string? CurrentUserAccount => LoginInfo?.LoginName;

        private string? _displayName;
        public string? DisplayName
        {
            get => _displayName ?? keyAutoMapper?.DisplayName;
            set
            {
                _displayName = value;
                if (keyAutoMapper != null) keyAutoMapper.DisplayName = value;
                RaisePropertyChanged();
            }
        }

        // 開發者設定區塊（全部支援 TwoWay Binding）
        private bool _enableVerboseLogging;
        public bool EnableVerboseLogging
        {
            get => _enableVerboseLogging;
            set => SetProperty(ref _enableVerboseLogging, value);
        }

        private bool _enablePerformanceMonitoring;
        public bool EnablePerformanceMonitoring
        {
            get => _enablePerformanceMonitoring;
            set => SetProperty(ref _enablePerformanceMonitoring, value);
        }

        private bool _showDebugInfo;
        public bool ShowDebugInfo
        {
            get => _showDebugInfo;
            set => SetProperty(ref _showDebugInfo, value);
        }

        private bool _bypassProductionChecks;
        public bool BypassProductionChecks
        {
            get => _bypassProductionChecks;
            set => SetProperty(ref _bypassProductionChecks, value);
        }

        private string? _githubUrl;
        public string? GithubUrl
        {
            get => _githubUrl ?? keyAutoMapper?.GithubUrl;
            set
            {
                _githubUrl = value;
                if (keyAutoMapper != null) keyAutoMapper.GithubUrl = value;
                RaisePropertyChanged();
            }
        }

        private string? _cnServiceUrl;
        public string? CNServiceUrl
        {
            get => _cnServiceUrl ?? keyAutoMapper?.CNServiceUrl;
            set
            {
                _cnServiceUrl = value;
                if (keyAutoMapper != null) keyAutoMapper.CNServiceUrl = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        #region 保存邏輯（完整版）
        private async Task SavePreferenceAsync()
        {
            try
            {
                if (keyAutoMapper == null)
                    throw new InvalidOperationException("KeyAutoMapper 未初始化");

                if (LoginInfo == null)
                    throw new InvalidOperationException("LoginInfo 未設定");

                var pairs = keyAutoMapper.GetKeyValuePairs();
                if (pairs == null || !pairs.Any())
                    return;

                // 1. 構建 IN 條件與參數
                var keys = pairs.Select(p => p.Key).ToList();
                string inClause = string.Join(",", keys.Select((_, i) => $"@key{i}"));
                var keyParams = keys.Select((k, i) => new SqlParameter($"@key{i}", k)).ToArray();

                // 2. 查詢已存在的鍵
                string checkSql = $"SELECT cg_key FROM SysConfig WHERE cg_key IN ({inClause}) AND cg_userAccount = @userAccount";
                var existingKeys = new HashSet<string>();

                var ds = await _dataService.GetSelectResultParameterizedQueryAsync(
                    "DetOB", checkSql, "", 0,
                    keyParams.Concat(new[] { new SqlParameter("@userAccount", LoginInfo.LoginName) }).ToArray());

                if (ds?.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    foreach (DataRow row in ds.Tables[0].Rows)
                    {
                        if (row["cg_key"] != DBNull.Value)
                            existingKeys.Add(row["cg_key"].ToString()!);
                    }
                }

                // 3. 分組
                var toUpdate = pairs.Where(p => existingKeys.Contains(p.Key)).ToList();
                var toInsert = pairs.Where(p => !existingKeys.Contains(p.Key)).ToList();

                // 4. 使用 TransactionScope 執行
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    // 更新已存在記錄
                    foreach (var pair in toUpdate)
                    {
                        string updateSql = "UPDATE SysConfig SET cg_value = @value WHERE cg_key = @key AND cg_userAccount = @userAccount";
                        var parameters = new[]
                        {
                            new SqlParameter("@key", pair.Key),
                            new SqlParameter("@value", pair.Value ?? ""),
                            new SqlParameter("@userAccount", LoginInfo.LoginName)
                        };

                        string result = await _dataService.ExecuteParameterizedQueryAsync("DetOB", updateSql, parameters);
                        if (result?.StartsWith("0") == true)
                            throw new Exception($"更新失敗：{pair.Key}");
                    }

                    // 插入新記錄
                    foreach (var pair in toInsert)
                    {
                        string insertSql = "INSERT INTO SysConfig (cg_key, cg_value, cg_userAccount, cd_creationTime) " +
                                           "VALUES (@key, @value, @userAccount, GETDATE())";

                        var parameters = new[]
                        {
                            new SqlParameter("@key", pair.Key),
                            new SqlParameter("@value", pair.Value ?? ""),
                            new SqlParameter("@userAccount", LoginInfo.LoginName)
                        };

                        string result = await _dataService.ExecuteParameterizedQueryAsync("DetOB", insertSql, parameters);
                        if (result?.StartsWith("0") == true)
                            throw new Exception($"插入失敗：{pair.Key}");
                    }

                    scope.Complete();
                }

                System.Diagnostics.Debug.WriteLine("✅ 用戶偏好設定儲存成功");
                // await _dialogService.ShowMessageAsync("成功", "偏好設定已儲存");   // 如有 dialogService 可自行加入
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存偏好設定失敗: {ex}");
            }
        }
        #endregion
    }
}