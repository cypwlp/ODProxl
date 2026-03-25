using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IUploadService
    {
        Task<string> UploadFileAsync(string filePath, CancellationToken cancellationToken = default);
        Task PushUploadAsync();
    }
}
