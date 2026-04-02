using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IUpdateService
    {
        Task UpdateODProxlAsync(string countryCode);

        /// <summary>
        /// 發布新的 DLL 更新版本（支持單平台或所有平台）
        /// </summary>
        Task<bool> PublishNewDllVersionAsync(string version, string dllFilePaths,
            string updateDescription, string codeDescription, string targetRid);

        /// <summary>
        /// 【新增】Velopack 發布：git commit + tag + push
        /// </summary>
        Task<bool> PublishVelopackVersionAsync(string version, string updateDescription, string codeDescription);
    }
}