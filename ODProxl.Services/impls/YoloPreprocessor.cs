using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
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

            // YOLOv8 / YOLOv11 官方預設使用 RGB，僅 YOLOv5 常使用 BGR
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
            using var skBitmap = LoadSkBitmap(image);
            var (letterboxed, ratio, padX, padY) = Letterbox(skBitmap, TargetWidth, TargetHeight);

            var tensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });

            unsafe
            {
                var info = new SKImageInfo(letterboxed.Width, letterboxed.Height, SKColorType.Bgra8888);
                using var pixmap = letterboxed.PeekPixels();

                byte* ptr = (byte*)pixmap.GetPixels().ToPointer();
                int stride = pixmap.RowBytes;

                for (int y = 0; y < TargetHeight; y++)
                {
                    for (int x = 0; x < TargetWidth; x++)
                    {
                        int offset = y * stride + x * 4;
                        byte b = ptr[offset + 0];
                        byte g = ptr[offset + 1];
                        byte r = ptr[offset + 2];

                        float rf = r / 255f;
                        float gf = g / 255f;
                        float bf = b / 255f;

                        if (_useBgr)
                        {
                            tensor[0, 0, y, x] = (bf - _mean[0]) / _std[0]; // B
                            tensor[0, 1, y, x] = (gf - _mean[1]) / _std[1]; // G
                            tensor[0, 2, y, x] = (rf - _mean[2]) / _std[2]; // R
                        }
                        else
                        {
                            tensor[0, 0, y, x] = (rf - _mean[0]) / _std[0]; // R
                            tensor[0, 1, y, x] = (gf - _mean[1]) / _std[1]; // G
                            tensor[0, 2, y, x] = (bf - _mean[2]) / _std[2]; // B
                        }
                    }
                }
            }

            letterboxed.Dispose();

            return new Dictionary<string, Tensor<float>> { { _inputName, tensor } };
        }

        private static SKBitmap LoadSkBitmap(object image)
        {
            switch (image)
            {
                case SKBitmap bmp:
                    return bmp.Copy(); // 複製一份，避免外部修改

                case string path when File.Exists(path):
                    using (var stream = File.OpenRead(path))
                        return SKBitmap.Decode(stream);

                case byte[] bytes:
                    return SKBitmap.Decode(bytes);

                case Stream stream:
                    stream.Position = 0;
                    return SKBitmap.Decode(stream);

                default:
                    throw new ArgumentException($"不支援的影像類型: {image?.GetType().Name}");
            }
        }

        /// <summary>
        /// 與 Ultralytics Python 完全一致的 Letterbox 實現（使用 SkiaSharp）
        /// </summary>
        private (SKBitmap letterboxed, float ratio, int padX, int padY) Letterbox(SKBitmap src, int targetW, int targetH)
        {
            float ratio = Math.Min((float)targetW / src.Width, (float)targetH / src.Height);

            int newW = (int)Math.Round(src.Width * ratio);
            int newH = (int)Math.Round(src.Height * ratio);

            int dw = targetW - newW;
            int dh = targetH - newH;

            int padX = (int)Math.Round(dw / 2.0f - 0.1f);
            int padY = (int)Math.Round(dh / 2.0f - 0.1f);

            // 建立目標畫布（灰色填充 114）
            var letterboxed = new SKBitmap(targetW, targetH);
            using var canvas = new SKCanvas(letterboxed);
            canvas.Clear(new SKColor(114, 114, 114));

            // 縮放並繪製
            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High
            };

            var destRect = new SKRect(padX, padY, padX + newW, padY + newH);
            canvas.DrawBitmap(src, destRect, paint);

            return (letterboxed, ratio, padX, padY);
        }
    }
}