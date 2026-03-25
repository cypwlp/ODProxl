using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class UpdateFile
    {
        public string? Path { get; set; }
        public string? Hash { get; set; }
        public long Size { get; set; }
    }
}
