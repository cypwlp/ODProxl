using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.ML.OnnxRuntime;
using ODProxl.EntityModels;
using ODProxl.Services;
using ODProxl.Services.impls;
using Prism.Mvvm;
using RemoteService;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace ODProxl.ViewModels.Pages
{
    public class ClassMarkPageViewModel : BindableBase, INavigationAware, IDisposable
    {
        private readonly string _imagesBaseUrl = "http://interior.topmix.net/info/system/software/ODProxl/Anotaion/images/";
        private readonly string _labelsBaseUrl = "http://interior.topmix.net/info/system/software/ODProxl/Anotaion/labels/";
        private readonly string _localCachePath = Path.Combine(AppContext.BaseDirectory, "Cache", "Images");
        private readonly string _modelsCachePath = Path.Combine(AppContext.BaseDirectory, "Cache", "Models");
        private readonly HttpClient _httpClient;
        private readonly IDataService _dataService;
        private Point _startPoint;
        private bool _isDragging;
        private Point _currentRectEnd;
        private List<Point> _currentPolygonPoints = new();
        private Point? _tempMovePoint;
        private bool _isPolygonMode;
        private double _imagePixelWidth;
        private double _imagePixelHeight;
        private int _currentImageIndex = -1;
        private string _statusText = "準備就緒";
        private string _mousePositionText = "X: --- Y: ---";
        private int _polygonPointCount;
        private Bitmap? _currentImage;
        private double _zoomLevel = 1.0;
        private Image? _imageControl;
        private Canvas? _canvas;
        private SKBitmap? _currentSkBitmap;
        private string? _currentModelPath;
        private LoginInfo? _loginInfo;
        public event Action? RequestResetZoom;
        private List<string> _currentModelClasses = new();

        public LoginInfo? LoginInfo
        {
            get => _loginInfo;
            set => SetProperty(ref _loginInfo, value);
        }
        public bool IsPolygonMode
        {
            get => _isPolygonMode;
            set
            {
                if (SetProperty(ref _isPolygonMode, value))
                    RaisePropertyChanged(nameof(NotIsPolygonMode));
            }
        }
        public bool NotIsPolygonMode => !IsPolygonMode;
        public double ImagePixelWidth { get => _imagePixelWidth; set => SetProperty(ref _imagePixelWidth, value); }
        public double ImagePixelHeight { get => _imagePixelHeight; set => SetProperty(ref _imagePixelHeight, value); }
        public int CurrentImageIndex { get => _currentImageIndex; set => SetProperty(ref _currentImageIndex, value); }
        public double ZoomLevel { get => _zoomLevel; set => SetProperty(ref _zoomLevel, value); }
        public int PolygonPointCount { get => _polygonPointCount; set => SetProperty(ref _polygonPointCount, value); }
        public Bitmap? CurrentImage { get => _currentImage; set => SetProperty(ref _currentImage, value); }
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string MousePositionText { get => _mousePositionText; set => SetProperty(ref _mousePositionText, value); }
        public string ModeText => IsPolygonMode ? "多邊形模式" : "矩形模式";
        public ObservableCollection<string> ExpectedImagePaths { get; } = new();
        public ObservableCollection<ClassItem> Classes { get; } = new();
        public ObservableCollection<Annotation> Annotations { get; } = new();
        public ClassItem? SelectedClass { get; set; }

        public DelegateCommand SetRectModeCommand { get; }
        public DelegateCommand SetPolygonModeCommand { get; }
        public AsyncDelegateCommand SaveAnnotationsCommand { get; }
        public DelegateCommand ResetZoomCommand { get; }
        public DelegateCommand CancelPolygonCommand { get; }
        public AsyncDelegateCommand PrevImageCommand { get; }
        public AsyncDelegateCommand NextImageCommand { get; }
        public DelegateCommand<Annotation> DeleteAnnotationCommand { get; }
        public AsyncDelegateCommand AutoAnnotateCommand { get; }

        public ClassMarkPageViewModel(IDataService dataService, HttpClient httpClient)
        {
            _dataService = dataService;
            _httpClient = httpClient;
            Directory.CreateDirectory(_localCachePath);
            Directory.CreateDirectory(_modelsCachePath);

            SetRectModeCommand = new DelegateCommand(() => IsPolygonMode = false);
            SetPolygonModeCommand = new DelegateCommand(() => IsPolygonMode = true);
            SaveAnnotationsCommand = new AsyncDelegateCommand(SaveAnnotationsForCurrentImageAsync);
            ResetZoomCommand = new DelegateCommand(() => RequestResetZoom?.Invoke());
            CancelPolygonCommand = new DelegateCommand(CancelCurrentPolygon);
            PrevImageCommand = new AsyncDelegateCommand(async () => { if (CurrentImageIndex > 0) await LoadImageAsync(CurrentImageIndex - 1); });
            NextImageCommand = new AsyncDelegateCommand(async () => { if (CurrentImageIndex < ExpectedImagePaths.Count - 1) await LoadImageAsync(CurrentImageIndex + 1); });
            DeleteAnnotationCommand = new DelegateCommand<Annotation>(ann =>
            {
                if (ann != null && Annotations.Contains(ann))
                {
                    Annotations.Remove(ann);
                    RedrawAllAnnotations();
                }
            });
            AutoAnnotateCommand = new AsyncDelegateCommand(RunAutoAnnotationAsync);

            Classes.Add(new ClassItem { Name = "車牌" });
            Classes.Add(new ClassItem { Name = "車身" });
            Classes.Add(new ClassItem { Name = "輪胎" });
            SelectedClass = Classes.FirstOrDefault();
        }

        private void CancelCurrentPolygon()
        {
            _currentPolygonPoints.Clear();
            PolygonPointCount = 0;
            _tempMovePoint = null;
            RedrawAllAnnotations();
        }

        public async Task ProcessPdfFolderAsync(string folderPath)
        {
            var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly);
            await ProcessPdfFilesAsync(pdfFiles);
        }
        public async Task ProcessPdfFileAsync(string filePath)
        {
            await ProcessPdfFilesAsync(new[] { filePath });
        }
        private async Task ProcessPdfFilesAsync(IEnumerable<string> pdfPaths)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExpectedImagePaths.Clear();
                Annotations.Clear();
                CurrentImage = null;
                CurrentImageIndex = -1;
                StatusText = "正在處理 PDF 文件...";
            });
            int totalProcessed = 0;
            foreach (var pdfPath in pdfPaths)
            {
                var pdfFileName = Path.GetFileNameWithoutExtension(pdfPath);
                int pageCount = 0;
                try
                {
                    using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(2480, 3508));
                    pageCount = docReader.GetPageCount();
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        StatusText = $"無法讀取 PDF：{Path.GetFileName(pdfPath)} - {ex.Message}");
                    continue;
                }
                for (int page = 0; page < pageCount; page++)
                {
                    string imageName = $"{pdfFileName}_p{(page + 1):D3}.png";
                    string imageHttpUrl = _imagesBaseUrl + imageName;
                    bool existsOnServer = await ImageExistsOnServerAsync(imageHttpUrl);
                    if (existsOnServer)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ExpectedImagePaths.Add(imageHttpUrl);
                            totalProcessed++;
                            StatusText = $"已從伺服器載入圖片 {imageName} ({totalProcessed} 張)";
                            if (ExpectedImagePaths.Count == 1)
                            {
                                CurrentImageIndex = 0;
                                _ = LoadImageAsync(0);
                            }
                        });
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => StatusText = $"正在轉換 300 DPI 圖片: {imageName}");
                        byte[] pngBytes = await RenderPdfPageToPngAsync(pdfPath, page);
                        await UploadImageToServerAsync(imageHttpUrl, pngBytes);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ExpectedImagePaths.Add(imageHttpUrl);
                            totalProcessed++;
                            StatusText = $"已轉換並上傳圖片 {imageName} ({totalProcessed} 張)";
                            if (ExpectedImagePaths.Count == 1)
                            {
                                CurrentImageIndex = 0;
                                _ = LoadImageAsync(0);
                            }
                        });
                    }
                }
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusText = $"處理完成，共 {totalProcessed} 張圖片（已自動同步至伺服器）");
        }
        private async Task<bool> ImageExistsOnServerAsync(string imageHttpUrl)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, imageHttpUrl);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        private async Task<byte[]> RenderPdfPageToPngAsync(string pdfPath, int pageIndex)
        {
            return await Task.Run(() =>
            {
                using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(2480, 3508));
                using var pageReader = docReader.GetPageReader(pageIndex);
                var rawBytes = pageReader.GetImage();
                int width = pageReader.GetPageWidth();
                int height = pageReader.GetPageHeight();
                var info = new SKImageInfo(width, height, SKColorType.Bgra8888);
                using var skData = SKData.CreateCopy(rawBytes);
                using var skImage = SKImage.FromPixels(info, skData);
                using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                encoded.SaveTo(ms);
                return ms.ToArray();
            });
        }
        private async Task UploadImageToServerAsync(string imageHttpUrl, byte[] pngBytes)
        {
            var content = new ByteArrayContent(pngBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            var response = await _httpClient.PutAsync(imageHttpUrl, content);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"上傳圖片失敗: HTTP {(int)response.StatusCode}");
        }
        public async Task LoadClassesFromEnabledModelAsync()
        {
            var (_, modelClasses) = await GetEnabledModelWithClassesAsync();
            _currentModelClasses = modelClasses;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Classes.Clear();

                if (modelClasses.Any())
                {
                    foreach (var name in modelClasses)
                        Classes.Add(new ClassItem { Name = name });
                }
                else
                {
                    // 後備類別
                    Classes.Add(new ClassItem { Name = "車牌" });
                    Classes.Add(new ClassItem { Name = "車身" });
                    Classes.Add(new ClassItem { Name = "輪胎" });
                    Debug.WriteLine("⚠️ 使用預設類別（classes.txt 載入失敗）");
                }

                SelectedClass = Classes.FirstOrDefault();
            });
        }

        private string GetLabelHttpUrl(string imageHttpPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(imageHttpPath) + ".json";
            return _labelsBaseUrl + fileName;
        }
        private async Task LoadAnnotationsForCurrentImageAsync()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ExpectedImagePaths.Count) return;
            var imagePath = ExpectedImagePaths[CurrentImageIndex];
            var labelHttpUrl = GetLabelHttpUrl(imagePath);
            try
            {
                var response = await _httpClient.GetAsync(labelHttpUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var dtos = JsonSerializer.Deserialize<List<AnnotationDto>>(json);
                    if (dtos != null)
                    {
                        Annotations.Clear();
                        foreach (var dto in dtos)
                        {
                            var ann = new Annotation
                            {
                                ClassName = dto.ClassName,
                                IsPolygon = dto.IsPolygon,
                                Points = dto.Points.Select(p => new Point(p[0], p[1])).ToList()
                            };
                            Annotations.Add(ann);
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Annotations.Clear();
                }
            }
            catch { }
        }
        private async Task SaveAnnotationsForCurrentImageAsync()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ExpectedImagePaths.Count)
            {
                StatusText = "沒有圖片可儲存";
                return;
            }
            var imagePath = ExpectedImagePaths[CurrentImageIndex];
            var labelHttpUrl = GetLabelHttpUrl(imagePath);
            try
            {
                var dtos = Annotations.Select(ann => new AnnotationDto
                {
                    ClassName = ann.ClassName,
                    IsPolygon = ann.IsPolygon,
                    Points = ann.Points.Select(p => new List<double> { p.X, p.Y }).ToList()
                }).ToList();
                string json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(labelHttpUrl, content);
                StatusText = response.IsSuccessStatusCode
                    ? "✅ 標註已成功儲存到伺服器"
                    : $"⚠️ 儲存失敗: HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 儲存失敗: {ex.Message}";
            }
        }

        private async Task<string> EnsureImageLocalAsync(string imageHttpUrl)
        {
            var fileName = Path.GetFileName(imageHttpUrl);
            var localPath = Path.Combine(_localCachePath, fileName);
            if (File.Exists(localPath)) return localPath;
            try
            {
                StatusText = $"正在從伺服器下載圖片: {fileName}";
                var bytes = await _httpClient.GetByteArrayAsync(imageHttpUrl);
                await File.WriteAllBytesAsync(localPath, bytes);
                StatusText = $"圖片下載完成: {fileName}";
                return localPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"無法從伺服器取得圖片 {fileName}：{ex.Message}");
            }
        }
        public async Task LoadImageAsync(int index)
        {
            if (index < 0 || index >= ExpectedImagePaths.Count) return;
            CurrentImageIndex = index;
            var httpPath = ExpectedImagePaths[index];
            try
            {
                var localPath = await EnsureImageLocalAsync(httpPath);
                using var stream = File.OpenRead(localPath);
                CurrentImage = new Bitmap(stream);
                _currentSkBitmap?.Dispose();
                _currentSkBitmap = SKBitmap.Decode(localPath);
                ImagePixelWidth = CurrentImage.PixelSize.Width;
                ImagePixelHeight = CurrentImage.PixelSize.Height;
                Annotations.Clear();
                _currentPolygonPoints.Clear();
                PolygonPointCount = 0;
                _isDragging = false;
                _tempMovePoint = null;
                await LoadAnnotationsForCurrentImageAsync();
                RedrawAllAnnotations();
                StatusText = $"已載入第 {index + 1} 張圖片（來自伺服器）";
            }
            catch (Exception ex)
            {
                StatusText = $"載入失敗: {ex.Message}";
            }
        }

        public void SetControls(Image? image, Canvas? canvas)
        {
            _imageControl = image;
            _canvas = canvas;
        }
        public void RedrawAllAnnotations()
        {
            if (_canvas == null) return;
            _canvas.Children.Clear();
            foreach (var ann in Annotations)
            {
                if (ann.IsPolygon && ann.Points.Count >= 3)
                {
                    var polygon = new Polygon
                    {
                        Points = new Points(ann.Points),
                        Stroke = Brushes.Red,
                        StrokeThickness = 3.5,
                        Fill = new SolidColorBrush(Colors.Red, 0.06),
                        StrokeJoin = PenLineJoin.Round
                    };
                    _canvas.Children.Add(polygon);
                }
                else if (!ann.IsPolygon && ann.Points.Count == 2)
                {
                    var p1 = ann.Points[0];
                    var p2 = ann.Points[1];
                    var rect = new Rectangle
                    {
                        Width = Math.Abs(p2.X - p1.X),
                        Height = Math.Abs(p2.Y - p1.Y),
                        Stroke = Brushes.Blue,
                        StrokeThickness = 3.5,
                        Fill = Brushes.Transparent
                    };
                    Canvas.SetLeft(rect, Math.Min(p1.X, p2.X));
                    Canvas.SetTop(rect, Math.Min(p1.Y, p2.Y));
                    _canvas.Children.Add(rect);
                }
            }
            if (IsPolygonMode && _currentPolygonPoints.Count > 0)
            {
                var tempPoints = new List<Point>(_currentPolygonPoints);
                if (_tempMovePoint.HasValue)
                    tempPoints.Add(_tempMovePoint.Value);
                if (tempPoints.Count >= 2)
                {
                    var tempPoly = new Polygon
                    {
                        Points = new Points(tempPoints),
                        Stroke = Brushes.Orange,
                        StrokeThickness = 3.5,
                        Fill = new SolidColorBrush(Colors.Orange, 0.08),
                        StrokeDashArray = new AvaloniaList<double> { 5, 3 }
                    };
                    _canvas.Children.Add(tempPoly);
                }
            }
            if (!IsPolygonMode && _isDragging && _currentRectEnd != default)
            {
                var p1 = _startPoint;
                var p2 = _currentRectEnd;
                var rect = new Rectangle
                {
                    Width = Math.Abs(p2.X - p1.X),
                    Height = Math.Abs(p2.Y - p1.Y),
                    Stroke = Brushes.Lime,
                    StrokeThickness = 3.5,
                    StrokeDashArray = new AvaloniaList<double> { 4, 2 },
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(rect, Math.Min(p1.X, p2.X));
                Canvas.SetTop(rect, Math.Min(p1.Y, p2.Y));
                _canvas.Children.Add(rect);
            }
        }

        public void OnPointerPressed(Point imagePixelPos)
        {
            if (!IsPolygonMode)
            {
                _startPoint = imagePixelPos;
                _currentRectEnd = imagePixelPos;
                _isDragging = true;
            }
            else
            {
                _currentPolygonPoints.Add(imagePixelPos);
                PolygonPointCount = _currentPolygonPoints.Count;
                _tempMovePoint = imagePixelPos;
            }
            RedrawAllAnnotations();
        }
        public void OnPointerPressedRight(Point imagePixelPos)
        {
            if (IsPolygonMode)
            {
                if (_currentPolygonPoints.Count >= 3)
                    FinishCurrentPolygon();
                else if (_currentPolygonPoints.Count > 0)
                {
                    _currentPolygonPoints.RemoveAt(_currentPolygonPoints.Count - 1);
                    PolygonPointCount = _currentPolygonPoints.Count;
                }
            }
            else
            {
                _isDragging = false;
            }
            RedrawAllAnnotations();
        }
        public void OnPointerMoved(Point imagePixelPos)
        {
            MousePositionText = $"X: {imagePixelPos.X:F1} Y: {imagePixelPos.Y:F1}";
            if (!IsPolygonMode && _isDragging)
                _currentRectEnd = imagePixelPos;
            else if (IsPolygonMode && _currentPolygonPoints.Count > 0)
                _tempMovePoint = imagePixelPos;
            RedrawAllAnnotations();
        }
        public void OnPointerReleased(Point imagePixelPos)
        {
            if (!IsPolygonMode && _isDragging)
            {
                _currentRectEnd = imagePixelPos;
                AddRectangleAnnotation();
                _isDragging = false;
            }
            RedrawAllAnnotations();
        }
        private void AddRectangleAnnotation()
        {
            if (_startPoint == default || _currentRectEnd == default) return;
            var ann = new Annotation
            {
                Points = new List<Point> { _startPoint, _currentRectEnd },
                IsPolygon = false,
                ClassName = SelectedClass?.Name ?? "未知"
            };
            Annotations.Add(ann);
        }
        private void FinishCurrentPolygon()
        {
            if (_currentPolygonPoints.Count < 3) return;
            var ann = new Annotation
            {
                Points = new List<Point>(_currentPolygonPoints),
                IsPolygon = true,
                ClassName = SelectedClass?.Name ?? "未知"
            };
            Annotations.Add(ann);
            _currentPolygonPoints.Clear();
            PolygonPointCount = 0;
            _tempMovePoint = null;
        }

        private async Task<(FileSystemItem? model, List<string> classes)> GetEnabledModelWithClassesAsync()
        {
            if (_loginInfo?.LoginName == null)
                return (null, new List<string>());

            try
            {
                var param = new SqlParameter("@LoginName", _loginInfo.LoginName);

                string? modelName = await _dataService.ScalarParamAsync("ODProxl",
                    "SELECT model_name FROM sys_models WHERE model_userAccount = @LoginName", param);

                string? modelPath = await _dataService.ScalarParamAsync("ODProxl",
                    "SELECT model_path FROM sys_models WHERE model_userAccount = @LoginName", param);

                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    Debug.WriteLine("⚠️ 資料庫中沒有已啟用的模型");
                    return (null, new List<string>());
                }

                var model = new FileSystemItem { Name = modelName ?? "", FullPath = modelPath };

                // 產生 classes.txt 的完整網址
                var classesUrl = modelPath.Replace(".onnx", ".txt", StringComparison.OrdinalIgnoreCase);
                Debug.WriteLine($"📄 正在載入 classes 檔案: {classesUrl}");

                List<string> classes = new();

                try
                {
                    var text = await _httpClient.GetStringAsync(classesUrl);
                    Debug.WriteLine($"✅ classes.txt 內容長度: {text.Length} 字元");

                    classes = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim())
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .ToList();

                    Debug.WriteLine($"✅ 成功載入 {classes.Count} 個類別");
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"❌ classes.txt 檔案不存在: {classesUrl}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 讀取 classes.txt 失敗: {ex.Message}");
                }

                return (model, classes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetEnabledModelWithClassesAsync 發生錯誤: {ex.Message}");
                return (null, new List<string>());
            }
        }
        private async Task<string> EnsureModelLocalAsync(string modelHttpUrl)
        {
            var fileName = Path.GetFileName(modelHttpUrl);
            var localPath = Path.Combine(_modelsCachePath, fileName);
            if (File.Exists(localPath)) return localPath;
            StatusText = $"正在下載 ONNX 模型 {fileName}（可能需幾秒～幾十秒）...";
            var bytes = await _httpClient.GetByteArrayAsync(modelHttpUrl);
            await File.WriteAllBytesAsync(localPath, bytes);
            StatusText = $"✅ 模型下載完成";
            return localPath;
        }
        private async Task RunAutoAnnotationAsync()
        {
            if (CurrentImage == null || _currentSkBitmap == null)
            {
                StatusText = "請先載入圖片";
                return;
            }
            var (enabledModel, modelClasses) = await GetEnabledModelWithClassesAsync();
            if (enabledModel == null)
            {
                StatusText = "❌ 沒有已啟用的 ONNX 模型，請先到「模型管理」頁面啟用一個模型";
                return;
            }
            var localModelPath = await EnsureModelLocalAsync(enabledModel.FullPath);
            using var tempSession = new InferenceSession(localModelPath);
            var preprocessor = YoloPreprocessor.FromSession(tempSession);
            var postprocessor = new YoloPostprocessor(
                confThreshold: 0.30f,
                iouThreshold: 0.45f,
                classNames: modelClasses.ToArray(),
                inputWidth: preprocessor.TargetWidth,
                inputHeight: preprocessor.TargetHeight,
                originalWidth: (int)ImagePixelWidth,
                originalHeight: (int)ImagePixelHeight);
            postprocessor.UpdateLetterboxParams((int)ImagePixelWidth, (int)ImagePixelHeight);
            using var inferenceService = new OnnxInferenceService(localModelPath, preprocessor, postprocessor);
            StatusText = $"🤖 正在使用 {enabledModel.Name} 進行 AI 自動標註...";
            var result = await inferenceService.PredictAsync(_currentSkBitmap);
            int added = 0;
            foreach (var box in result.Boxes)
            {
                if (box.Confidence < 0.25f) continue;
                var ann = new Annotation
                {
                    IsPolygon = false,
                    Points = new List<Point>
                    {
                        new Point(box.X, box.Y),
                        new Point(box.X + box.Width, box.Y + box.Height)
                    },
                    ClassName = string.IsNullOrWhiteSpace(box.Label) ? "偵測物件" : box.Label
                };
                Annotations.Add(ann);
                added++;
            }
            RedrawAllAnnotations();
            StatusText = $"✅ AI 自動標註完成！新增 {added} 個矩形（可手動刪除或修正）";
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (navigationContext.Parameters.ContainsKey("LoginInfo"))
                LoginInfo = navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
            try
            {
                await LoadClassesFromEnabledModelAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入失敗: {ex.Message}");
                // 載入預設類別
            }
        }

        public void Dispose()
        {
            _currentSkBitmap?.Dispose();
        }
    }
}