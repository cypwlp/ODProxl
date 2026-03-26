using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class OnnxModelInfo
    {
        // 模型基础信息
        public string ModelPath { get; set; }
        public string ModelName { get; set; }
        public string ProducerName { get; set; }
        public string GraphName { get; set; }
        public string Description { get; set; }
        public long Version { get; set; }
        public List<OnnxTensorInfo> Inputs { get; set; } = new List<OnnxTensorInfo>();
        public List<OnnxTensorInfo> Outputs { get; set; } = new List<OnnxTensorInfo>();
        public Dictionary<string, string> CustomMetadata { get; set; } = new Dictionary<string, string>();
        public string InputTensorName => Inputs.FirstOrDefault()?.TensorName;
        public string OutputTensorName => Outputs.FirstOrDefault()?.TensorName;
        public int[] InputDimensions => Inputs.FirstOrDefault()?.Dimensions?.ToArray();
        public int[] OutputDimensions => Outputs.FirstOrDefault()?.Dimensions?.ToArray();
    }
}
