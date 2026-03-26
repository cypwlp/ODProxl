using System.Threading;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IUploadService
    {
        Task PushUploadAsync(string version, string description, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 自動建立 git tag 並 push（觸發 GitHub Actions 建置 Velopack）
        /// </summary>
        Task CreateAndPushGitTagAsync(string version, string description, string repoPath);
    }
}