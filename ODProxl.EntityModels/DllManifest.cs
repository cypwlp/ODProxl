using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
   public class DllManifest
    {
        public string Version { get; set; } = string.Empty;
        public List<DllInfo> Dlls { get; set; } = new();
    }
}
