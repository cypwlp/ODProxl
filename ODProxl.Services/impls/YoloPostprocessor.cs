using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ODProxl.EntityModels;
using System;
using System.Collections.Generic;
using System.Linq;

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

        // Letterbox 参数（与预处理保持一致）
        private float _ratio;
        private int _padX;
        private int _padY;

        public YoloPostprocessor(float confThreshold = 0.30f,
                                 float iouThreshold = 0.45f,
                                 string[]? classNames = null,
                                 int inputWidth = 640,
                                 int inputHeight = 640,
                                 int originalWidth = 640,
                                 int originalHeight = 640)
        {
            _confThreshold = confThreshold;
            _iouThreshold = iouThreshold;
            _classNames = classNames ?? Array.Empty<string>();
            _inputWidth = inputWidth;
            _inputHeight = inputHeight;
            _originalWidth = originalWidth;
            _originalHeight = originalHeight;

            // 计算初始 letterbox 参数
            UpdateLetterboxParams(_originalWidth, _originalHeight);
        }

        /// <summary>
        /// 與 Preprocessor 完全一致的 letterbox 參數計算（關鍵！）
        /// </summary>
        public void UpdateLetterboxParams(int width, int height)
        {
            float ratio = Math.Min((float)_inputWidth / width, (float)_inputHeight / height);
            _ratio = ratio;

            // ★★★ 與 Preprocessor 完全一樣的 Round + padding 計算
            int newW = (int)Math.Round(width * ratio);
            int newH = (int)Math.Round(height * ratio);

            int dw = _inputWidth - newW;
            int dh = _inputHeight - newH;

            _padX = (int)Math.Round(dw / 2.0f - 0.1f);
            _padY = (int)Math.Round(dh / 2.0f - 0.1f);
        }
        public OnnxResult Process(IReadOnlyList<NamedOnnxValue> outputs)
        {
            var format = _cachedFormat ?? AnalyzeOutputs(outputs);
            _cachedFormat = format;

            var detectionTensor = outputs.First(o => o.Name == format.DetectionName).AsTensor<float>();
            var boxes = ParseDetections(detectionTensor, format);

            if (format.IsSegmentation && !string.IsNullOrEmpty(format.ProtoName))
            {
                var protoTensor = outputs.First(o => o.Name == format.ProtoName).AsTensor<float>();
                boxes = ApplyMasks(boxes, protoTensor, format);
            }

            var masks = boxes.Where(b => b.Mask != null)
                             .Select(b => FlattenMask(b.Mask))
                             .ToArray();

            return new OnnxResult { Boxes = boxes, Masks = masks };
        }

        private OutputFormatInfo AnalyzeOutputs(IReadOnlyList<NamedOnnxValue> outputs)
        {
            var detection = outputs.FirstOrDefault(o => o.Name.Equals("output0", StringComparison.OrdinalIgnoreCase))
                         ?? outputs.Where(o => o.AsTensor<float>()?.Dimensions.Length == 3)
                                   .OrderByDescending(o => o.AsTensor<float>().Length)
                                   .FirstOrDefault();

            if (detection == null)
                throw new InvalidOperationException("找不到檢測輸出 tensor。");

            int[] shape = detection.AsTensor<float>().Dimensions.ToArray();

            if (shape.Length != 3)
            {
                var shapeStr = string.Join("\n", outputs.Select(o => $"{o.Name} → shape=[{string.Join(",", o.AsTensor<float>().Dimensions.ToArray())}]"));
                throw new NotSupportedException($"不支援的輸出形狀: [{string.Join(",", shape)}]\n\n所有輸出：\n{shapeStr}");
            }

            var info = new OutputFormatInfo
            {
                DetectionName = detection.Name,
                IsSegmentation = false
            };

            bool isHwc = shape[1] > 1000 && shape[2] < 300;
            int channels = isHwc ? shape[2] : shape[1];
            int numDetections = isHwc ? shape[1] : shape[2];

            var protoCandidate = outputs.FirstOrDefault(o =>
                o.Name.Equals("output1", StringComparison.OrdinalIgnoreCase) ||
                o.Name.Contains("proto", StringComparison.OrdinalIgnoreCase) ||
                o.Name.Contains("mask", StringComparison.OrdinalIgnoreCase));

            int maskChannels = 0;
            if (protoCandidate != null)
            {
                var protoShape = protoCandidate.AsTensor<float>().Dimensions.ToArray();
                if (protoShape.Length == 4)
                {
                    maskChannels = protoShape[1];
                    info.MaskChannels = maskChannels;
                    info.ProtoName = protoCandidate.Name;
                    info.IsSegmentation = true;
                }
            }

            if (info.IsSegmentation)
            {
                info.NumClasses = channels - 4 - maskChannels;
                info.HasMaskCoeff = true;
            }
            else
            {
                info.NumClasses = channels - 4;
                info.HasMaskCoeff = false;
            }

            if (isHwc)
            {
                if (info.HasMaskCoeff)
                    info.Format = OutputFormat.Yolov8HwcWithMask;
                else
                    info.Format = OutputFormat.Yolov8Hwc;
            }
            else
            {
                info.Format = OutputFormat.Yolov8Chw;
            }

            if (info.IsSegmentation && protoCandidate != null)
            {
                var protoShape = protoCandidate.AsTensor<float>().Dimensions.ToArray();
                if (protoShape.Length == 4)
                {
                    info.ProtoHeight = protoShape[2];
                    info.ProtoWidth = protoShape[3];
                }
            }

            return info;
        }

        private List<BoundingBox> ParseDetections(Tensor<float> tensor, OutputFormatInfo format)
        {
            var boxes = new List<BoundingBox>();
            var dims = tensor.Dimensions.ToArray();

            // 直接使用已更新的成员变量 _ratio, _padX, _padY
            float ratio = _ratio;
            int padX = _padX;
            int padY = _padY;

            switch (format.Format)
            {
                case OutputFormat.Yolov8Chw:
                    int numChw = dims[2];
                    for (int i = 0; i < numChw; i++)
                    {
                        int predLength = 4 + format.NumClasses + (format.HasMaskCoeff ? format.MaskChannels : 0);
                        float[] pred = new float[predLength];
                        for (int j = 0; j < predLength; j++)
                            pred[j] = tensor[0, j, i];

                        float[] box = pred.Take(4).ToArray();
                        float[] scores = pred.Skip(4).Take(format.NumClasses).ToArray();

                        float maxScore = scores.Max();
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(scores, maxScore);
                        string label = classId < _classNames.Length ? _classNames[classId] : $"class_{classId}";

                        float cx = box[0];
                        float cy = box[1];
                        float w = box[2];
                        float h = box[3];

                        // 坐标转换：先减去填充，再除以缩放比例
                        float x = (cx - padX) / ratio;
                        float y = (cy - padY) / ratio;
                        float width = w / ratio;
                        float height = h / ratio;

                        var boxObj = new BoundingBox
                        {
                            X = x,
                            Y = y,
                            Width = width,
                            Height = height,
                            Label = label,
                            Confidence = maxScore
                        };

                        if (format.HasMaskCoeff)
                        {
                            float[] maskCoeffs = pred.Skip(4 + format.NumClasses).Take(format.MaskChannels).ToArray();
                            boxObj.MaskCoeffs = maskCoeffs;
                        }

                        boxes.Add(boxObj);
                    }
                    break;

                case OutputFormat.Yolov8Hwc:
                    int numHwc = dims[1];
                    for (int i = 0; i < numHwc; i++)
                    {
                        float[] pred = new float[4 + format.NumClasses];
                        for (int j = 0; j < pred.Length; j++)
                            pred[j] = tensor[0, i, j];

                        float[] box = pred.Take(4).ToArray();
                        float[] scores = pred.Skip(4).ToArray();

                        float maxScore = scores.Max();
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(scores, maxScore);
                        string label = classId < _classNames.Length ? _classNames[classId] : $"class_{classId}";

                        float cx = box[0];
                        float cy = box[1];
                        float w = box[2];
                        float h = box[3];

                        float x = (cx - padX) / ratio;
                        float y = (cy - padY) / ratio;
                        float width = w / ratio;
                        float height = h / ratio;

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

                case OutputFormat.Yolov8HwcWithMask:
                    int numMask = dims[1];
                    int maskCoeffDim = dims[2] - 4 - format.NumClasses;
                    for (int i = 0; i < numMask; i++)
                    {
                        float[] pred = new float[4 + format.NumClasses + maskCoeffDim];
                        for (int j = 0; j < pred.Length; j++)
                            pred[j] = tensor[0, i, j];

                        float[] box = pred.Take(4).ToArray();
                        float[] scores = pred.Skip(4).Take(format.NumClasses).ToArray();

                        float maxScore = scores.Max();
                        if (maxScore < _confThreshold) continue;

                        int classId = Array.IndexOf(scores, maxScore);
                        string label = classId < _classNames.Length ? _classNames[classId] : $"class_{classId}";

                        float cx = box[0];
                        float cy = box[1];
                        float w = box[2];
                        float h = box[3];

                        float x = (cx - padX) / ratio;
                        float y = (cy - padY) / ratio;
                        float width = w / ratio;
                        float height = h / ratio;

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
        Yolov8Chw,
        Yolov8Hwc,
        Yolov8HwcWithMask
    }
}