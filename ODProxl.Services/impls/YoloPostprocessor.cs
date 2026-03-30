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
    public class YoloPostprocessor : IPostprocessor
    {
        private float _confThreshold;
        private float _iouThreshold;
        private string[] _classNames;    // 外部提供，如果为空则使用索引
        private int _inputWidth;
        private int _inputHeight;
        private int _originalWidth;
        private int _originalHeight;

        // 缓存上一次解析的结果（假设同一模型多次调用）
        private OutputFormatInfo _cachedFormat;

        public YoloPostprocessor(float confThreshold = 0.25f, float iouThreshold = 0.45f,
                                 string[] classNames = null,
                                 int inputWidth = 640, int inputHeight = 640,
                                 int originalWidth = 640, int originalHeight = 640)
        {
            _confThreshold = confThreshold;
            _iouThreshold = iouThreshold;
            _classNames = classNames ?? Array.Empty<string>();
            _inputWidth = inputWidth;
            _inputHeight = inputHeight;
            _originalWidth = originalWidth;
            _originalHeight = originalHeight;
        }

        public OnnxResult Process(IReadOnlyList<NamedOnnxValue> outputs)
        {
            // 1. 分析输出结构（第一次调用时分析，后续复用）
            var format = _cachedFormat ?? AnalyzeOutputs(outputs);
            _cachedFormat = format;

            // 2. 提取检测结果
            var detectionTensor = outputs.First(o => o.Name == format.DetectionName).AsTensor<float>();
            var boxes = ParseDetections(detectionTensor, format);

            // 3. 如果是分割模型，应用掩码
            if (format.IsSegmentation)
            {
                var protoTensor = outputs.First(o => o.Name == format.ProtoName).AsTensor<float>();
                boxes = ApplyMasks(boxes, protoTensor, format);
            }

            // 4. 转换为最终结果
            var masks = boxes.Where(b => b.Mask != null)
                             .Select(b => FlattenMask(b.Mask))
                             .ToArray();

            return new OnnxResult
            {
                Boxes = boxes,
                Masks = masks
            };
        }

        private OutputFormatInfo AnalyzeOutputs(IReadOnlyList<NamedOnnxValue> outputs)
        {
            // 1. 查找可能的检测输出
            var detectionCandidates = outputs
                .Where(o => o.Name.Contains("output") || o.Name.Contains("detect") || o.Name.Contains("yolo"))
                .ToList();

            if (detectionCandidates.Count == 0)
                throw new InvalidOperationException("No detection output found.");

            // 优先选择形状最合理的（检测输出通常元素数量很大，比如 > 1000）
            var detection = detectionCandidates
                .OrderByDescending(o => o.AsTensor<float>().Length)
                .First();

            var detectionShape = detection.AsTensor<float>().Dimensions.ToArray();
            var info = new OutputFormatInfo
            {
                DetectionName = detection.Name,
                IsSegmentation = false
            };

            // 判断形状格式
            if (detectionShape.Length == 3)
            {
                // 格式1: [1, N, 5+numClasses]  -> YOLOv5 或 YOLOv8 HWC
                if (detectionShape[2] > 100 && detectionShape[1] > 1000)
                {
                    // 实际上是 [1, N, 85]，需要看哪个维度是变量
                    if (detectionShape[2] == 85)
                    {
                        info.Format = OutputFormat.Yolov5;
                        info.NumClasses = 80;
                    }
                    else if (detectionShape[2] > 85 && detectionShape[1] > 1000)
                    {
                        // 可能是 [1, N, 4+numClasses+maskCoeff]
                        info.Format = OutputFormat.Yolov8HwcWithMask;
                        info.NumClasses = detectionShape[2] - 4 - 32; // 假设掩码系数 32
                    }
                    else if (detectionShape[1] > 1000 && detectionShape[2] > 4)
                    {
                        // 通用 HWC: [1, N, 4+numClasses]
                        info.Format = OutputFormat.Yolov8Hwc;
                        info.NumClasses = detectionShape[2] - 4;
                    }
                }
                // 格式2: [1, 4+numClasses, N] -> YOLOv8 CHW
                else if (detectionShape[1] > 4 && detectionShape[2] > 1000)
                {
                    info.Format = OutputFormat.Yolov8Chw;
                    info.NumClasses = detectionShape[1] - 4;
                    // 检查是否有掩码系数（4+numClasses+32）
                    if (detectionShape[1] == 4 + 80 + 32) info.HasMaskCoeff = true;
                }
                else
                {
                    throw new NotSupportedException($"Unsupported detection shape: [{string.Join(",", detectionShape)}]");
                }
            }
            else
            {
                throw new NotSupportedException("Only 3D detection outputs are supported.");
            }

            // 2. 判断是否为分割模型：有 proto 输出 且 检测输出包含掩码系数
            var protoCandidate = outputs.FirstOrDefault(o => o.Name.Contains("proto") || o.Name.Contains("mask"));
            if (protoCandidate != null && info.HasMaskCoeff)
            {
                info.IsSegmentation = true;
                info.ProtoName = protoCandidate.Name;
                var protoShape = protoCandidate.AsTensor<float>().Dimensions.ToArray();
                // proto 形状应为 [1, mask_channels, H, W]
                info.MaskChannels = protoShape[1];
                info.ProtoHeight = protoShape[2];
                info.ProtoWidth = protoShape[3];
            }

            // 3. 如果 classNames 未提供，生成默认标签
            if (_classNames == null || _classNames.Length != info.NumClasses)
            {
                // 尝试从模型元数据读取（此处省略，可以扩展）
                _classNames = Enumerable.Range(0, info.NumClasses).Select(i => i.ToString()).ToArray();
            }

            return info;
        }

        private List<BoundingBox> ParseDetections(Tensor<float> tensor, OutputFormatInfo format)
        {
            var dims = tensor.Dimensions.ToArray();
            var boxes = new List<BoundingBox>();

            switch (format.Format)
            {
                case OutputFormat.Yolov5: // [1, N, 85]
                    int numBoxes = dims[1];
                    for (int i = 0; i < numBoxes; i++)
                    {
                        float objConf = tensor[0, i, 4];
                        if (objConf < _confThreshold) continue;

                        float[] clsScores = new float[format.NumClasses];
                        for (int j = 0; j < format.NumClasses; j++)
                            clsScores[j] = tensor[0, i, 5 + j];

                        float maxScore = clsScores.Max() * objConf;
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(clsScores, clsScores.Max());
                        string label = classId < _classNames.Length ? _classNames[classId] : classId.ToString();

                        float x = tensor[0, i, 0] * _originalWidth;
                        float y = tensor[0, i, 1] * _originalHeight;
                        float w = tensor[0, i, 2] * _originalWidth;
                        float h = tensor[0, i, 3] * _originalHeight;

                        boxes.Add(new BoundingBox
                        {
                            X = x - w / 2,
                            Y = y - h / 2,
                            Width = w,
                            Height = h,
                            Label = label,
                            Confidence = maxScore
                        });
                    }
                    break;

                case OutputFormat.Yolov8Chw: // [1, 4+numClasses, N]
                    int numBoxesChw = dims[2];
                    for (int i = 0; i < numBoxesChw; i++)
                    {
                        float[] pred = new float[4 + format.NumClasses];
                        for (int j = 0; j < pred.Length; j++)
                            pred[j] = tensor[0, j, i];

                        float[] box = pred.Take(4).ToArray();
                        float[] scores = pred.Skip(4).ToArray();
                        float maxScore = scores.Max();
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(scores, maxScore);
                        string label = classId < _classNames.Length ? _classNames[classId] : classId.ToString();

                        float cx = box[0];
                        float cy = box[1];
                        float w = box[2];
                        float h = box[3];

                        float x = (cx - w / 2) * _originalWidth / _inputWidth;
                        float y = (cy - h / 2) * _originalHeight / _inputHeight;
                        float width = w * _originalWidth / _inputWidth;
                        float height = h * _originalHeight / _inputHeight;

                        boxes.Add(new BoundingBox
                        {
                            X = x,
                            Y = y,
                            Width = width,
                            Height = height,
                            Label = label,
                            Confidence = maxScore
                        });
                    }
                    break;

                case OutputFormat.Yolov8Hwc: // [1, N, 4+numClasses]
                    int numBoxesHwc = dims[1];
                    for (int i = 0; i < numBoxesHwc; i++)
                    {
                        float[] pred = new float[4 + format.NumClasses];
                        for (int j = 0; j < pred.Length; j++)
                            pred[j] = tensor[0, i, j];

                        float[] box = pred.Take(4).ToArray();
                        float[] scores = pred.Skip(4).ToArray();
                        float maxScore = scores.Max();
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(scores, maxScore);
                        string label = classId < _classNames.Length ? _classNames[classId] : classId.ToString();

                        float cx = box[0];
                        float cy = box[1];
                        float w = box[2];
                        float h = box[3];

                        float x = (cx - w / 2) * _originalWidth / _inputWidth;
                        float y = (cy - h / 2) * _originalHeight / _inputHeight;
                        float width = w * _originalWidth / _inputWidth;
                        float height = h * _originalHeight / _inputHeight;

                        boxes.Add(new BoundingBox
                        {
                            X = x,
                            Y = y,
                            Width = width,
                            Height = height,
                            Label = label,
                            Confidence = maxScore
                        });
                    }
                    break;

                case OutputFormat.Yolov8HwcWithMask: // [1, N, 4+numClasses+32]
                    // 类似 HWC，但最后 32 维是掩码系数，需要保存
                    int numBoxesMask = dims[1];
                    int maskCoeffDim = dims[2] - 4 - format.NumClasses; // 通常为 32
                    for (int i = 0; i < numBoxesMask; i++)
                    {
                        float[] pred = new float[4 + format.NumClasses + maskCoeffDim];
                        for (int j = 0; j < pred.Length; j++)
                            pred[j] = tensor[0, i, j];

                        float[] box = pred.Take(4).ToArray();
                        float[] scores = pred.Skip(4).Take(format.NumClasses).ToArray();
                        float maxScore = scores.Max();
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(scores, maxScore);
                        string label = classId < _classNames.Length ? _classNames[classId] : classId.ToString();

                        float cx = box[0];
                        float cy = box[1];
                        float w = box[2];
                        float h = box[3];

                        float x = (cx - w / 2) * _originalWidth / _inputWidth;
                        float y = (cy - h / 2) * _originalHeight / _inputHeight;
                        float width = w * _originalWidth / _inputWidth;
                        float height = h * _originalHeight / _inputHeight;

                        var boxObj = new BoundingBox
                        {
                            X = x,
                            Y = y,
                            Width = width,
                            Height = height,
                            Label = label,
                            Confidence = maxScore
                        };

                        // 保存掩码系数（稍后用于生成掩码）
                        float[] maskCoeffs = pred.Skip(4 + format.NumClasses).ToArray();
                        // 这里需要扩展 BoundingBox 来存储系数，我们新增一个字段 MaskCoeffs
                        boxObj.MaskCoeffs = maskCoeffs;
                        boxes.Add(boxObj);
                    }
                    break;
            }

            // 应用 NMS
            return Nms(boxes, _iouThreshold);
        }

        private List<BoundingBox> ApplyMasks(List<BoundingBox> boxes, Tensor<float> protoTensor, OutputFormatInfo format)
        {
            // protoTensor 形状: [1, maskChannels, H_proto, W_proto]
            // boxes 中的每个框已有 MaskCoeffs (长度 = maskChannels)
            // 掩码计算: mask = sigmoid(proto * coeffs) 然后上采样到原图尺寸
            int maskChannels = format.MaskChannels;
            int protoH = format.ProtoHeight;
            int protoW = format.ProtoWidth;

            foreach (var box in boxes)
            {
                if (box.MaskCoeffs == null || box.MaskCoeffs.Length != maskChannels)
                    continue;

                // 创建掩码矩阵 (protoH x protoW)
                var maskRaw = new float[protoH, protoW];
                for (int y = 0; y < protoH; y++)
                {
                    for (int x = 0; x < protoW; x++)
                    {
                        float sum = 0;
                        for (int c = 0; c < maskChannels; c++)
                        {
                            sum += protoTensor[0, c, y, x] * box.MaskCoeffs[c];
                        }
                        maskRaw[y, x] = 1.0f / (1.0f + (float)Math.Exp(-sum)); // sigmoid
                    }
                }

                // 上采样到原图尺寸（实际需要缩放到原始图像尺寸，并裁剪到框内）
                // 简化：直接缩放掩码到目标尺寸，然后二值化
                var mask = ResizeMask(maskRaw, box, protoH, protoW);
                box.Mask = mask;
            }
            return boxes;
        }

        private byte[,] ResizeMask(float[,] maskRaw, BoundingBox box, int protoH, int protoW)
        {
            // 将原始掩码从 (protoH, protoW) 缩放到原始图像尺寸，并裁剪到框区域
            // 实际实现使用双线性插值，这里简化为最邻近
            int maskW = (int)(box.Width);
            int maskH = (int)(box.Height);
            var mask = new byte[maskH, maskW];
            for (int y = 0; y < maskH; y++)
            {
                for (int x = 0; x < maskW; x++)
                {
                    int protoX = (int)((float)x / maskW * protoW);
                    int protoY = (int)((float)y / maskH * protoH);
                    mask[y, x] = maskRaw[protoY, protoX] > 0.5f ? (byte)255 : (byte)0;
                }
            }
            return mask;
        }

        private List<BoundingBox> Nms(List<BoundingBox> boxes, float iouThreshold)
        {
            boxes = boxes.OrderByDescending(b => b.Confidence).ToList();
            var result = new List<BoundingBox>();
            while (boxes.Any())
            {
                var best = boxes[0];
                result.Add(best);
                boxes.RemoveAt(0);
                boxes.RemoveAll(b => Iou(best, b) > iouThreshold);
            }
            return result;
        }

        private float Iou(BoundingBox a, BoundingBox b)
        {
            float x1 = Math.Max(a.X, b.X);
            float y1 = Math.Max(a.Y, b.Y);
            float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = a.Width * a.Height;
            float areaB = b.Width * b.Height;
            return inter / (areaA + areaB - inter);
        }

        private byte[] FlattenMask(byte[,] mask)
        {
            int rows = mask.GetLength(0);
            int cols = mask.GetLength(1);
            byte[] flat = new byte[rows * cols];
            Buffer.BlockCopy(mask, 0, flat, 0, flat.Length);
            return flat;
        }
    }

    // 辅助类
    internal class OutputFormatInfo
    {
        public string DetectionName { get; set; }
        public string ProtoName { get; set; }
        public OutputFormat Format { get; set; }
        public int NumClasses { get; set; }
        public bool HasMaskCoeff { get; set; }
        public bool IsSegmentation { get; set; }
        public int MaskChannels { get; set; }
        public int ProtoHeight { get; set; }
        public int ProtoWidth { get; set; }
    }

    internal enum OutputFormat
    {
        Yolov5,
        Yolov8Chw,
        Yolov8Hwc,
        Yolov8HwcWithMask
    }
}
