using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IPreprocessor
    {        /// <summary>
             /// 将原始图片转换为模型输入张量
             /// </summary>
             /// <param name="image">原始图片（如 byte[] 或 Bitmap）</param>
             /// <returns>模型输入字典（名称 -> 张量）</returns>
        Dictionary<string, Tensor<float>> Process(object image);
    }
}
