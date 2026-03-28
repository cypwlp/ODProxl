using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class DllInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;     // SHA256 小写无分隔符
        public long Size { get; set; }
        public string Url { get; set; } = string.Empty;      // 如果为空则使用 baseUrl + FileName
    }
}
