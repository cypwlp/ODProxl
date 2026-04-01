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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace ODProxl.ViewModels.Pages
{
    public class UserPreferencePageViewModel : BindableBase, INavigationAware
    {
        private readonly IDataService _dataService;
        private LoginInfo? loginInfo;
        private KeyAutoMapper? _keyAutoMapper;

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

        public UserPreferencePageViewModel(IDataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            keyAutoMapper = new KeyAutoMapper();
            SaveCommandAsync = new DelegateCommand(async () => await SavePreferenceAsync());
        }

        private async Task SavePreferenceAsync()
        {
            try
            {
                if (keyAutoMapper == null || LoginInfo == null) return;
                var pairs = keyAutoMapper.GetKeyValuePairs();
                if (pairs == null || !pairs.Any()) return;

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var keys = pairs.Select(p => p.Key).ToList();
                    string inClause = string.Join(",", keys.Select((_, i) => $"@key{i}"));
                    var keyParams = keys.Select((k, i) => new SqlParameter($"@key{i}", k)).ToArray();

                    string checkSql = $"SELECT cg_key FROM sys_config WHERE cg_key IN ({inClause}) AND cg_userAccount = @userAccount";
                    var ds = await _dataService.QueryParamAsync("ODProxl", checkSql, "", 0,
                        keyParams.Concat(new[] { new SqlParameter("@userAccount", LoginInfo.LoginName) }).ToArray());

                    var existingKeys = new HashSet<string>();
                    if (ds?.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        foreach (DataRow row in ds.Tables[0].Rows)
                        {
                            if (row["cg_key"] != DBNull.Value)
                                existingKeys.Add(row["cg_key"].ToString()!);
                        }
                    }

                    var toUpdate = pairs.Where(p => existingKeys.Contains(p.Key)).ToList();
                    var toInsert = pairs.Where(p => !existingKeys.Contains(p.Key)).ToList();

                    foreach (var pair in toUpdate)
                    {
                        string updateSql = "UPDATE sys_config SET cg_value = @value WHERE cg_key = @key AND cg_userAccount = @userAccount";
                        var parameters = new[]
                        {
                            new SqlParameter("@key", pair.Key),
                            new SqlParameter("@value", pair.Value ?? ""),
                            new SqlParameter("@userAccount", LoginInfo.LoginName)
                        };
                        await _dataService.ExecParamAsync("ODProxl", updateSql, parameters);
                    }

                    foreach (var pair in toInsert)
                    {
                        string insertSql = "INSERT INTO sys_config (cg_key, cg_value, cg_userAccount, cd_creationTime) VALUES (@key, @value, @userAccount, GETDATE())";
                        var parameters = new[]
                        {
                            new SqlParameter("@key", pair.Key),
                            new SqlParameter("@value", pair.Value ?? ""),
                            new SqlParameter("@userAccount", LoginInfo.LoginName)
                        };
                        await _dataService.ExecParamAsync("ODProxl", insertSql, parameters);
                    }

                    scope.Complete();
                }
                Debug.WriteLine("✅ 用戶偏好設定儲存成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"儲存偏好設定失敗: {ex}");
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoginInfo = navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
        }
    }
}