using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace ODProxl.Services.impls
{
    public class YoloPreprocessor : IPreprocessor
    {
        private readonly string _inputName;
        public readonly int TargetWidth;
        public readonly int TargetHeight;

        private readonly bool _useBgr;
        private readonly float[] _mean;
        private readonly float[] _std;

        public static YoloPreprocessor FromSession(InferenceSession session, bool forceBgr = false)
        {
            if (session.InputMetadata.Count == 0)
                throw new InvalidOperationException("模型沒有輸入節點！");

            var inputMeta = session.InputMetadata.First();
            var dims = inputMeta.Value.Dimensions;
            bool isChw = dims.Length >= 4 && (dims[1] == 3 || dims[1] == 1);
            int height = isChw ? (int)dims[2] : (int)dims[1];
            int width = isChw ? (int)dims[3] : (int)dims[2];

            bool useBgr = forceBgr ||
                          session.ModelMetadata.ProducerName?.Contains("YOLOv5", StringComparison.OrdinalIgnoreCase) == true;

            return new YoloPreprocessor(inputMeta.Key, width, height, useBgr);
        }

        public YoloPreprocessor(string inputName, int targetWidth, int targetHeight,
                                bool useBgr = false, float[]? mean = null, float[]? std = null)
        {
            _inputName = inputName;
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            _useBgr = useBgr;
            _mean = mean ?? new[] { 0f, 0f, 0f };
            _std = std ?? new[] { 1f, 1f, 1f };
        }

        public Dictionary<string, Tensor<float>> Process(object image)
        {
            using var matSrc = LoadMat(image);

            // 使用 OpenCV 完全相同的 letterbox（與 best.pt 100% 一致）
            var (letterboxedMat, ratio, padX, padY) = LetterboxMat(matSrc, TargetWidth, TargetHeight);

            var tensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });

            unsafe
            {
                var dataPtr = (byte*)letterboxedMat.DataPointer;
                long step = letterboxedMat.Step();
                for (int y = 0; y < TargetHeight; y++)
                {
                    for (int x = 0; x < TargetWidth; x++)
                    {
                        byte b = dataPtr[y * step + x * 3 + 0];
                        byte g = dataPtr[y * step + x * 3 + 1];
                        byte r = dataPtr[y * step + x * 3 + 2];

                        // 归一化
                        float rf = r / 255f;
                        float gf = g / 255f;
                        float bf = b / 255f;

                        // 根据 _useBgr 决定通道顺序
                        if (_useBgr)
                        {
                            tensor[0, 0, y, x] = (bf - _mean[0]) / _std[0];
                            tensor[0, 1, y, x] = (gf - _mean[1]) / _std[1];
                            tensor[0, 2, y, x] = (rf - _mean[2]) / _std[2];
                        }
                        else
                        {
                            tensor[0, 0, y, x] = (rf - _mean[0]) / _std[0];
                            tensor[0, 1, y, x] = (gf - _mean[1]) / _std[1];
                            tensor[0, 2, y, x] = (bf - _mean[2]) / _std[2];
                        }
                    }
                }
            }

            return new Dictionary<string, Tensor<float>> { { _inputName, tensor } };
        }

        private static Mat LoadMat(object image)
        {
            Mat result;
            switch (image)
            {
                case Mat mat:
                    result = mat.Clone();
                    break;
                case SKBitmap bmp:
                    result = SKBitmapToMat(bmp);
                    break;
                case string path when File.Exists(path):
                    result = Cv2.ImRead(path, ImreadModes.Color);
                    break;
                case byte[] bytes:
                    result = Cv2.ImDecode(bytes, ImreadModes.Color);
                    break;
                case Stream stream:
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        result = Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
                    }
                    break;
                default:
                    throw new ArgumentException($"不支援的影像類型: {image?.GetType().Name}");
            }
            return result;
        }

        private static Mat SKBitmapToMat(SKBitmap bmp)
{
    using var skImage = SKImage.FromBitmap(bmp);
    using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
    return Cv2.ImDecode(encoded.ToArray(), ImreadModes.Color);
}

/// <summary>
/// 與 Ultralytics Python 完全一致的 letterbox（使用 OpenCV INTER_LINEAR）
/// </summary>
private (Mat letterboxed, float ratio, int padX, int padY) LetterboxMat(Mat src, int targetW, int targetH)
{
    float ratio = Math.Min((float)targetW / src.Width, (float)targetH / src.Height);

    int newW = (int)Math.Round(src.Width * ratio);
    int newH = (int)Math.Round(src.Height * ratio);

    int dw = targetW - newW;
    int dh = targetH - newH;

    int padX = (int)Math.Round(dw / 2.0f - 0.1f);
    int padY = (int)Math.Round(dh / 2.0f - 0.1f);

    using var resized = new Mat();
    Cv2.Resize(src, resized, new Size(newW, newH), interpolation: InterpolationFlags.Linear);

    var padded = new Mat(targetH, targetW, MatType.CV_8UC3, new Scalar(114, 114, 114));
    resized.CopyTo(padded[new Rect(padX, padY, newW, newH)]);

    return (padded, ratio, padX, padY);
}
    }
}