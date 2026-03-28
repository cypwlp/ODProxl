using Prism.Mvvm;
using System.Collections.Generic;

namespace ODProxl.EntityModels
{
    public class KeyAutoMapper : BindableBase
    {
        public KeyAutoMapper()
        {
#if DEBUG
            IsDeveloperMode = true;
#else
            IsDeveloperMode = false;
#endif
        }

        // 所有要儲存到 SysConfig 的屬性
        public bool IsDeveloperMode { get; set; }
        public string? ModelBaseUrl { get; set; }
        public string? DisplayName { get; set; }
        public string? GithubUrl { get; set; }
        public string? CNServiceUrl { get; set; }

        public bool EnableVerboseLogging { get; set; }
        public bool EnablePerformanceMonitoring { get; set; }
        public bool ShowDebugInfo { get; set; }
        public bool BypassProductionChecks { get; set; }

        /// <summary>
        /// 將所有設定轉成要存入 SysConfig 的鍵值對
        /// </summary>
        public List<KeyValuePair<string, string>> GetKeyValuePairs()
        {
            return new List<KeyValuePair<string, string>>
            {
                new("model_base_url", ModelBaseUrl ?? ""),
                new("display_name", DisplayName ?? ""),
                new("github_url", GithubUrl ?? ""),
                new("cn_service_url", CNServiceUrl ?? ""),
                new("enable_verbose_logging", EnableVerboseLogging.ToString()),
                new("enable_performance_monitoring", EnablePerformanceMonitoring.ToString()),
                new("show_debug_info", ShowDebugInfo.ToString()),
                new("bypass_production_checks", BypassProductionChecks.ToString())
            };
        }
    }
}