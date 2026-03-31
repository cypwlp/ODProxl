using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ODProxl.EntityModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services.impls
{
    public class OnnxModelInspector : IOnnxModelInspector
    {
        public Task<OnnxModelInfo> GetModelInfoAsync(InferenceSession session, string modelPath)
        {
            var metadata = session.ModelMetadata;

            var info = new OnnxModelInfo
            {
                ModelPath = modelPath,
                ModelName = Path.GetFileNameWithoutExtension(modelPath),
                ProducerName = metadata.ProducerName,
                GraphName = metadata.GraphName,
                Description = metadata.Description,
                Version = metadata.Version,
                CustomMetadata = metadata.CustomMetadataMap?.ToDictionary(kv => kv.Key, kv => kv.Value)
                                 ?? new Dictionary<string, string>()
            };

            foreach (var input in session.InputMetadata)
            {
                info.Inputs.Add(new OnnxTensorInfo
                {
                    TensorName = input.Key,
                    DataType = GetTensorElementType(input.Value.ElementType),
                    Dimensions = input.Value.Dimensions.ToList()
                });
            }

            foreach (var output in session.OutputMetadata)
            {
                info.Outputs.Add(new OnnxTensorInfo
                {
                    TensorName = output.Key,
                    DataType = GetTensorElementType(output.Value.ElementType),
                    Dimensions = output.Value.Dimensions.ToList()
                });
            }

            return Task.FromResult(info);
        }

        private static TensorElementType GetTensorElementType(Type type)
        {
            if (type == typeof(float)) return TensorElementType.Float;
            if (type == typeof(double)) return TensorElementType.Double;
            if (type == typeof(int)) return TensorElementType.Int32;
            if (type == typeof(long)) return TensorElementType.Int64;
            if (type == typeof(bool)) return TensorElementType.Bool;
            if (type == typeof(byte)) return TensorElementType.UInt8;
            if (type == typeof(sbyte)) return TensorElementType.Int8;
            if (type == typeof(short)) return TensorElementType.Int16;
            if (type == typeof(ushort)) return TensorElementType.UInt16;
            if (type == typeof(uint)) return TensorElementType.UInt32;
            if (type == typeof(ulong)) return TensorElementType.UInt64;
            if (type == typeof(string)) return TensorElementType.String;
            throw new NotSupportedException($"Unsupported ONNX type: {type.FullName}");
        }
    }
}
