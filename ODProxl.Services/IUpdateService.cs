using System;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IUpdateService
    {
        Task UpdateODProxlAsync(string countryCode);

        /// <summary>
        /// 发布新的 DLL 更新版本（支持单平台或所有平台）
        /// </summary>
        Task<bool> PublishNewDllVersionAsync(string version, string dllFilePaths,
            string updateDescription, string codeDescription, string targetRid);
    }
}