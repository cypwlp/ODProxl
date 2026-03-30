using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services.impls
{
    public class YoloPreprocessor : IPreprocessor
    {
        private readonly string _inputName;
        public readonly int _targetWidth;
        public readonly int _targetHeight;
        private readonly bool _useBgr;
        private readonly float[] _mean;
        private readonly float[] _std;

        /// <summary>
        /// 通过 InferenceSession 自动创建预处理器（推荐方式）
        /// </summary>
        public static YoloPreprocessor FromSession(InferenceSession session, bool forceBgr = false)
        {
            if (session.InputMetadata.Count == 0)
                throw new InvalidOperationException("模型没有输入节点！");

            // 通常 YOLO 模型只有一个输入，取第一个
            var inputMeta = session.InputMetadata.First();

            var dims = inputMeta.Value.Dimensions;

            bool isChw = dims.Length >= 4 && (dims[1] == 3 || dims[1] == 1);
            int height = isChw ? dims[2] : dims[1];
            int width = isChw ? dims[3] : dims[2];

            bool useBgr = forceBgr ||
                          session.ModelMetadata.ProducerName?.Contains("YOLOv5", StringComparison.OrdinalIgnoreCase) == true;

            return new YoloPreprocessor(inputMeta.Key, width, height, useBgr);
        }

        public YoloPreprocessor(string inputName, int targetWidth, int targetHeight,
                                bool useBgr = false, float[] mean = null, float[] std = null)
        {
            _inputName = inputName;
            _targetWidth = targetWidth;
            _targetHeight = targetHeight;
            _useBgr = useBgr;
            _mean = mean ?? new[] { 0f, 0f, 0f };
            _std = std ?? new[] { 1f, 1f, 1f };
        }

        public Dictionary<string, Tensor<float>> Process(object image)
        {
            using var skBitmap = LoadSkiaBitmap(image);
            var (resized, _, _, _) = Letterbox(skBitmap, _targetWidth, _targetHeight);

            var tensor = new DenseTensor<float>(new[] { 1, 3, _targetHeight, _targetWidth });

            for (int y = 0; y < _targetHeight; y++)
            {
                for (int x = 0; x < _targetWidth; x++)
                {
                    var pixel = resized.GetPixel(x, y);

                    float r = pixel.Red / 255f;
                    float g = pixel.Green / 255f;
                    float b = pixel.Blue / 255f;

                    if (_useBgr)
                    {
                        tensor[0, 0, y, x] = (b - _mean[0]) / _std[0];  // B
                        tensor[0, 1, y, x] = (g - _mean[1]) / _std[1];  // G
                        tensor[0, 2, y, x] = (r - _mean[2]) / _std[2];  // R
                    }
                    else
                    {
                        tensor[0, 0, y, x] = (r - _mean[0]) / _std[0];  // R
                        tensor[0, 1, y, x] = (g - _mean[1]) / _std[1];  // G
                        tensor[0, 2, y, x] = (b - _mean[2]) / _std[2];  // B
                    }
                }
            }

            return new Dictionary<string, Tensor<float>> { { _inputName, tensor } };
        }

        private static SKBitmap LoadSkiaBitmap(object image)
        {
            return image switch
            {
                SKBitmap bmp => bmp,
                string path when File.Exists(path) => SKBitmap.Decode(path),
                byte[] bytes => SKBitmap.Decode(bytes),
                Stream stream => SKBitmap.Decode(stream),
                _ => throw new ArgumentException($"不支持的图像类型: {image?.GetType().Name}")
            };
        }

        private (SKBitmap resized, int padX, int padY, float scale) Letterbox(SKBitmap src, int targetW, int targetH)
        {
            float ratio = Math.Min((float)targetW / src.Width, (float)targetH / src.Height);
            int newW = (int)(src.Width * ratio);
            int newH = (int)(src.Height * ratio);

            int padX = (targetW - newW) / 2;
            int padY = (targetH - newH) / 2;

            var info = new SKImageInfo(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul);
            var resized = new SKBitmap(info);

            using var canvas = new SKCanvas(resized);
            canvas.Clear(new SKColor(114, 114, 114));   // YOLO 常用的灰色 padding

            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
            var destRect = SKRect.Create(padX, padY, newW, newH);
            canvas.DrawBitmap(src, destRect, paint);

            return (resized, padX, padY, ratio);
        }
    }
}
