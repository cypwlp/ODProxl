using Microsoft.ML.OnnxRuntime;
using ODProxl.EntityModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IOnnxModelInspector
    {
        Task<OnnxModelInfo> GetModelInfoAsync(InferenceSession session, string modelPath);
    }
}
