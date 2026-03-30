using Microsoft.ML.OnnxRuntime;
using ODProxl.EntityModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services.impls
{
    public class OnnxModelAnalyzer : IOnnxModelAnalyzer
    {
        private readonly IOnnxModelInspector _inspector;

        public OnnxModelAnalyzer(IOnnxModelInspector inspector)
        {
            _inspector = inspector;
        }

        public async Task<OnnxAnalysisResult> AnalyzeAsync(string modelPath, bool deepAnalysis = false)
        {
            using var session = new InferenceSession(modelPath);
            var baseInfo = await _inspector.GetModelInfoAsync(session, modelPath);

            var result = new OnnxAnalysisResult
            {
                ModelPath = baseInfo.ModelPath,
                ModelName = baseInfo.ModelName,
                ProducerName = baseInfo.ProducerName,
                GraphName = baseInfo.GraphName,
                Description = baseInfo.Description,
                Version = baseInfo.Version,
                CustomMetadata = baseInfo.CustomMetadata,
                Inputs = baseInfo.Inputs,
                Outputs = baseInfo.Outputs,
                FileSize = new FileInfo(modelPath).Length
            };

            if (deepAnalysis)
            {
                // 深度分析需要 ONNX Runtime 底层 API，此处仅示意
                result.CompatibilityNotes = "深度分析需要 ONNX Runtime 1.14+ 并启用 Model API。";
            }

            return result;
        }
    }
}
