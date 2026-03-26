using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class OnnxAnalysisResult: OnnxModelInfo
    {
        public long FileSize { get; set; }
        public List<string> OperatorTypes { get; set; } = new List<string>();
        public long EstimatedParameterCount { get; set; }
        public bool HasDynamicInput => Inputs?.Any(i => i.HasDynamicDimension) ?? false;
        public bool HasDynamicOutput => Outputs?.Any(o => o.HasDynamicDimension) ?? false;
        public string CompatibilityNotes { get; set; }
    }
}
