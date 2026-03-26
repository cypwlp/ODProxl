using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    public class OnnxTensorInfo
    {
        public string? TensorName { get; set; }               // 张量名称
        public TensorElementType DataType { get; set; } // 元素类型
        public IReadOnlyList<int>? Dimensions { get; set; } // 形状（可能包含 -1）
        public bool HasDynamicDimension => Dimensions?.Any(d => d == -1) ?? false;
    }
}
