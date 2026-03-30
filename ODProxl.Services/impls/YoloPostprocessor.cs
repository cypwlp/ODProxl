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
        private readonly float _confThreshold;
        private readonly float _iouThreshold;
        private string[] _classNames;
        private readonly int _inputWidth;
        private readonly int _inputHeight;
        private readonly int _originalWidth;
        private readonly int _originalHeight;

        private OutputFormatInfo? _cachedFormat;

        public YoloPostprocessor(float confThreshold = 0.30f, float iouThreshold = 0.45f,
                                 string[]? classNames = null,
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
            var format = _cachedFormat ?? AnalyzeOutputs(outputs);
            _cachedFormat = format;

            var detectionTensor = outputs.First(o => o.Name == format.DetectionName).AsTensor<float>();
            var boxes = ParseDetections(detectionTensor, format);

            if (format.IsSegmentation)
            {
                var protoTensor = outputs.First(o => o.Name == format.ProtoName).AsTensor<float>();
                boxes = ApplyMasks(boxes, protoTensor, format);
            }

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
            // 收集所有 float tensor 資訊（方便除錯）
            var allInfo = outputs
                .Where(o => o.AsTensor<float>() != null)
                .Select(o => new
                {
                    o.Name,
                    Shape = o.AsTensor<float>().Dimensions.ToArray(),
                    Size = o.AsTensor<float>().Length
                })
                .ToList();

            // ==================== 修正重點：正確識別 detection 與 proto ====================
            // 1. 先找名稱為 output0 的 tensor（YOLOv11 標準 detection head）
            var detection = outputs.FirstOrDefault(o => o.Name.Equals("output0", StringComparison.OrdinalIgnoreCase));

            // 2. 如果沒有 output0，就找所有 3D tensor，並取元素數量最大的
            if (detection == null)
            {
                var threeDTensors = outputs
                    .Where(o => o.AsTensor<float>()?.Dimensions.Length == 3)
                    .ToList();

                if (threeDTensors.Count > 0)
                {
                    detection = threeDTensors.OrderByDescending(o => o.AsTensor<float>().Length).First();
                }
                else
                {
                    // 最後 fallback：取最大的 tensor
                    detection = outputs
                        .Where(o => o.AsTensor<float>() != null)
                        .OrderByDescending(o => o.AsTensor<float>().Length)
                        .FirstOrDefault();
                }
            }

            if (detection == null)
                throw new InvalidOperationException("模型沒有可用的 float tensor 輸出。");

            var detectionShape = detection.AsTensor<float>().Dimensions.ToArray();

            if (detectionShape.Length != 3)
            {
                var shapeStr = string.Join("\n", allInfo.Select(x => $"   • {x.Name} → shape=[{string.Join(",", x.Shape)}]  size={x.Size}"));
                throw new NotSupportedException(
                    $"⚠️ 只支援 3D 檢測輸出（YOLO 標準格式）。\n\n" +
                    $"目前最大/選擇的 tensor '{detection.Name}' 的形狀是 [{string.Join(",", detectionShape)}]\n\n" +
                    $"模型所有輸出 tensor：\n{shapeStr}\n\n" +
                    $"你的模型是 YOLO11s 匯出的 ONNX，請確認使用以下指令匯出：\n" +
                    "yolo export model=yolo11s-seg.pt format=onnx opset=17");
            }

            var info = new OutputFormatInfo
            {
                DetectionName = detection.Name,
                IsSegmentation = false
            };

            // ====================== 形狀判斷（支援 YOLOv11） ======================
            int dim1 = detectionShape[1];
            int dim2 = detectionShape[2];

            bool isHwc = dim1 > 1000 && dim2 < 300;   // [1, N, C] 格式
            int channels = isHwc ? dim2 : dim1;

            info.NumClasses = channels - 4;

            // 判斷是否為分割模型（有 mask coefficients）
            if (channels > 4 + 100)
            {
                info.HasMaskCoeff = true;
                info.NumClasses = channels - 4 - 32;   // YOLOv11-seg 通常是 32
                info.Format = isHwc ? OutputFormat.Yolov8HwcWithMask : OutputFormat.Yolov8Chw;
            }
            else if (isHwc)
            {
                info.Format = OutputFormat.Yolov8Hwc;
            }
            else
            {
                info.Format = OutputFormat.Yolov8Chw;
            }

            // ====================== 找 proto mask（YOLOv11-seg 的 output1） ======================
            var protoCandidate = outputs.FirstOrDefault(o =>
                o.Name.Equals("output1", StringComparison.OrdinalIgnoreCase) ||
                o.Name.Contains("proto", StringComparison.OrdinalIgnoreCase) ||
                o.Name.Contains("mask", StringComparison.OrdinalIgnoreCase));

            if (protoCandidate != null && info.HasMaskCoeff)
            {
                info.IsSegmentation = true;
                info.ProtoName = protoCandidate.Name;

                var protoShape = protoCandidate.AsTensor<float>().Dimensions.ToArray();
                if (protoShape.Length == 4)
                {
                    info.MaskChannels = protoShape[1];
                    info.ProtoHeight = protoShape[2];
                    info.ProtoWidth = protoShape[3];
                }
            }

            // ====================== 類別名稱 ======================
            if (_classNames.Length != info.NumClasses || _classNames.Length == 0)
            {
                _classNames = Enumerable.Range(0, Math.Max(1, info.NumClasses))
                                        .Select(i => $"class_{i}").ToArray();
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

                case OutputFormat.Yolov8Chw: // [1, 4+numClasses, N] ← 你的模型是這個格式
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
                    int numBoxesMask = dims[1];
                    int maskCoeffDim = dims[2] - 4 - format.NumClasses;
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
                        float[] maskCoeffs = pred.Skip(4 + format.NumClasses).ToArray();
                        boxObj.MaskCoeffs = maskCoeffs;
                        boxes.Add(boxObj);
                    }
                    break;
            }

            return Nms(boxes, _iouThreshold);
        }

        private List<BoundingBox> ApplyMasks(List<BoundingBox> boxes, Tensor<float> protoTensor, OutputFormatInfo format)
        {
            int maskChannels = format.MaskChannels;
            int protoH = format.ProtoHeight;
            int protoW = format.ProtoWidth;

            foreach (var box in boxes)
            {
                if (box.MaskCoeffs == null || box.MaskCoeffs.Length != maskChannels)
                    continue;

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
                        maskRaw[y, x] = 1.0f / (1.0f + (float)Math.Exp(-sum));
                    }
                }

                var mask = ResizeMask(maskRaw, box, protoH, protoW);
                box.Mask = mask;
            }
            return boxes;
        }

        private byte[,] ResizeMask(float[,] maskRaw, BoundingBox box, int protoH, int protoW)
        {
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

    internal class OutputFormatInfo
    {
        public string DetectionName { get; set; } = string.Empty;
        public string ProtoName { get; set; } = string.Empty;
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