using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class UpdateManifest
    {
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<UpdateFile> Files { get; set; }
    }
}
