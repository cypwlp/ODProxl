using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Docnet.Core;
using Docnet.Core.Models;
using ODProxl.EntityModels;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Path = System.IO.Path;

namespace ODProxl.ViewModels.Pages
{
    public class ClassMarkPageViewModel : BindableBase, INavigationAware
    {
       #region INavigationAware Members
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }
        public void OnNavigatedFrom(NavigationContext navigationContext){}
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }
        #endregion

        #region 字段
        private readonly string imagesUrl = "http://interior.topmix.net/info/system/software/ODProxl/Anotaion/images/";
        private readonly string labelsUrl = "http://interior.topmix.net/info/system/software/ODProxl/Anotaion/labels/";
        private Point _startPoint;
        private bool _isDragging;
        private Point _currentRectEnd;
        private List<Point> _currentPolygonPoints = new();
        private Point? _tempMovePoint;
        private bool _isPolygonMode;
        private double _imagePixelWidth;
        private double _imagePixelHeight;
        public bool NotIsPolygonMode => !IsPolygonMode;
        private int _currentImageIndex = -1;
        public string ModeText => IsPolygonMode ? "多邊形模式" : "矩形模式";
        private string _statusText = "準備就緒";
        private string _mousePositionText = "X: ---   Y: ---";
        private int _polygonPointCount;
        private Bitmap? _currentImage;
        public ObservableCollection<string> ExpectedImagePaths { get; } = new();
        public ObservableCollection<ClassItem> Classes { get; } = new();
        public ObservableCollection<Annotation> Annotations { get; } = new();
        private Annotation? _selectedAnnotation;
        private double _zoomLevel = 1.0;
        private Image? _imageControl;
        private Canvas? _canvas;
        public event Action? RequestResetZoom;
        private ClassItem? _selectedClass;

        public ClassMarkPageViewModel()
        {
            SetRectModeCommand = new DelegateCommand(() => IsPolygonMode = false);
            SetPolygonModeCommand = new DelegateCommand(() => IsPolygonMode = true);

            SaveAnnotationsCommand = new AsyncDelegateCommand(SaveAnnotationsForCurrentImageAsync);

            ResetZoomCommand = new DelegateCommand(() => RequestResetZoom?.Invoke());

            CancelPolygonCommand = new DelegateCommand(() =>
            {
                _currentPolygonPoints.Clear();
                PolygonPointCount = 0;
                _tempMovePoint = null;
                RedrawAllAnnotations();
            });

            PrevImageCommand = new AsyncDelegateCommand(async () =>
            {
                if (CurrentImageIndex > 0)
                    await LoadImageAsync(CurrentImageIndex - 1);
            });

            NextImageCommand = new AsyncDelegateCommand(async () =>
            {
                if (CurrentImageIndex < ExpectedImagePaths.Count - 1)
                    await LoadImageAsync(CurrentImageIndex + 1);
            });

            DeleteAnnotationCommand = new DelegateCommand<Annotation>(ann =>
            {
                if (ann != null && Annotations.Contains(ann))
                {
                    Annotations.Remove(ann);
                    RedrawAllAnnotations();
                }
            });

            // 初始化類別（可自行擴充）
            Classes.Add(new ClassItem { Name = "車牌" });
            Classes.Add(new ClassItem { Name = "車身" });
            Classes.Add(new ClassItem { Name = "輪胎" });
            SelectedClass = Classes.FirstOrDefault();
        }
        #endregion

        #region 属性
        public bool IsPolygonMode
        {
            get => _isPolygonMode;
            set
            {
                if (SetProperty(ref _isPolygonMode, value))
                    RaisePropertyChanged(nameof(NotIsPolygonMode));
            }
        }
        public double ImagePixelWidth
        {
            get => _imagePixelWidth;
            set => SetProperty(ref _imagePixelWidth, value);
        }

        public double ImagePixelHeight
        {
            get => _imagePixelHeight;
            set => SetProperty(ref _imagePixelHeight, value);
        }

        public int CurrentImageIndex
        {
            get => _currentImageIndex;
            set => SetProperty(ref _currentImageIndex, value);
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, value);
        }

        public int PolygonPointCount
        {
            get => _polygonPointCount;
            set => SetProperty(ref _polygonPointCount, value);
        }

        public Bitmap? CurrentImage
        {
            get => _currentImage;
            set => SetProperty(ref _currentImage, value);
        }
        public Annotation? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set => SetProperty(ref _selectedAnnotation, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }
        public string MousePositionText
        {
            get => _mousePositionText;
            set => SetProperty(ref _mousePositionText, value);
        }

        public ClassItem? SelectedClass
        {
            get => _selectedClass;
            set => SetProperty(ref _selectedClass, value);
        }
        #endregion


        #region Commands
        public DelegateCommand? SetRectModeCommand { get; }
        public DelegateCommand? SetPolygonModeCommand { get; }
        public AsyncDelegateCommand? SaveAnnotationsCommand { get; }
        public DelegateCommand? ResetZoomCommand { get; }
        public DelegateCommand? CancelPolygonCommand { get; }
        public AsyncDelegateCommand? PrevImageCommand { get; }
        public AsyncDelegateCommand? NextImageCommand { get; }
        public DelegateCommand<Annotation>? DeleteAnnotationCommand { get; }
        #endregion

        #region PDF處理
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
                    {
                        StatusText = $"無法讀取 PDF：{Path.GetFileName(pdfPath)} - {ex.Message}";
                    });
                    continue;
                }

                for (int page = 0; page < pageCount; page++)
                {
                    string imageName = $"{pdfFileName}_p{(page + 1):D3}.png";
                    string imagePath = Path.Combine(imagesUrl, imageName);

                    bool exists = File.Exists(imagePath);
                    if (!exists)
                    {
                        await Task.Run(() =>
                        {
                            Directory.CreateDirectory(imagesUrl);
                            using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(2480, 3508));
                            using var pageReader = docReader.GetPageReader(page);
                            var rawBytes = pageReader.GetImage();
                            int width = pageReader.GetPageWidth();
                            int height = pageReader.GetPageHeight();

                            var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888);
                            using var skData = SKData.CreateCopy(rawBytes);
                            using var skImage = SKImage.FromPixels(imageInfo, skData);
                            using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);

                            using var fs = File.OpenWrite(imagePath);
                            data.SaveTo(fs);
                        }).ConfigureAwait(false);
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ExpectedImagePaths.Add(imagePath);
                        totalProcessed++;
                        StatusText = $"已處理 {totalProcessed} 張圖片...";

                        if (ExpectedImagePaths.Count == 1)
                        {
                            CurrentImageIndex = 0;
                            _ = LoadImageAsync(0);
                        }
                    });
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"處理完成，共 {totalProcessed} 張圖片。";
                if (ExpectedImagePaths.Count == 0)
                    StatusText = "沒有成功處理任何圖片。";
            });
        }
        #endregion

        #region 載入圖片與標註、保存標註
        public void RedrawAllAnnotations()
        {
            if (_canvas == null || CurrentImage == null)
                return;

            _canvas.Children.Clear();

            // 已儲存的標註
            foreach (var ann in Annotations)
            {
                if (ann.IsPolygon && ann.Points.Count >= 3)
                {
                    var polyline = new Polyline
                    {
                        Points = new Points(ann.Points),
                        Stroke = Brushes.Red,
                        StrokeThickness = 3,
                        StrokeJoin = PenLineJoin.Round
                    };
                    _canvas.Children.Add(polyline);
                }
                else if (!ann.IsPolygon && ann.Points.Count == 2)
                {
                    var p1 = ann.Points[0];
                    var p2 = ann.Points[1];

                    var left = Math.Min(p1.X, p2.X);
                    var top = Math.Min(p1.Y, p2.Y);
                    var width = Math.Abs(p2.X - p1.X);
                    var height = Math.Abs(p2.Y - p1.Y);

                    var rect = new Rectangle
                    {
                        Width = width,
                        Height = height,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 3,
                        Fill = Brushes.Transparent
                    };

                    Canvas.SetLeft(rect, left);
                    Canvas.SetTop(rect, top);
                    _canvas.Children.Add(rect);
                }
            }

            // 正在繪製的臨時多邊形
            if (IsPolygonMode && _currentPolygonPoints.Count > 0)
            {
                var tempPoints = new List<Point>(_currentPolygonPoints);
                if (_tempMovePoint.HasValue)
                    tempPoints.Add(_tempMovePoint.Value);

                if (tempPoints.Count >= 2)
                {
                    var tempPoly = new Polyline
                    {
                        Points = new Points(tempPoints),
                        Stroke = Brushes.Orange,
                        StrokeThickness = 3,
                        StrokeDashArray = new AvaloniaList<double> { 4, 2 }
                    };
                    _canvas.Children.Add(tempPoly);
                }
            }

            // 正在拖曳的臨時矩形
            if (!IsPolygonMode && _isDragging && _currentRectEnd != default)
            {
                var p1 = _startPoint;
                var p2 = _currentRectEnd;

                var left = Math.Min(p1.X, p2.X);
                var top = Math.Min(p1.Y, p2.Y);
                var width = Math.Abs(p2.X - p1.X);
                var height = Math.Abs(p2.Y - p1.Y);

                var tempRect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = Brushes.Lime,
                    StrokeThickness = 3,
                    StrokeDashArray = new AvaloniaList<double> { 4, 2 },
                    Fill = Brushes.Transparent
                };

                Canvas.SetLeft(tempRect, left);
                Canvas.SetTop(tempRect, top);
                _canvas.Children.Add(tempRect);
            }
        }
        private async Task LoadImageAsync(int index)
        {
            if (index < 0 || index >= ExpectedImagePaths.Count) return;

            CurrentImageIndex = index;
            var path = ExpectedImagePaths[index];

            try
            {
                using var stream = File.OpenRead(path);
                CurrentImage = new Bitmap(stream);

                // 新增：設定原始像素尺寸
                ImagePixelWidth = CurrentImage.PixelSize.Width;
                ImagePixelHeight = CurrentImage.PixelSize.Height;

                Annotations.Clear();
                _currentPolygonPoints.Clear();
                PolygonPointCount = 0;
                _isDragging = false;
                _tempMovePoint = null;

                await LoadAnnotationsForCurrentImageAsync();
                RedrawAllAnnotations();

                StatusText = $"已載入第 {index + 1} 張圖片";
                MousePositionText = "X: ---   Y: ---";
            }
            catch (Exception ex)
            {
                StatusText = $"載入失敗: {ex.Message}";
            }
        }

        private async Task LoadAnnotationsForCurrentImageAsync()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ExpectedImagePaths.Count) return;

            string imagePath = ExpectedImagePaths[CurrentImageIndex];
            string labelPath = Path.Combine(labelsUrl, Path.GetFileNameWithoutExtension(imagePath) + ".json");

            if (!File.Exists(labelPath)) return;

            try
            {
                string json = await File.ReadAllTextAsync(labelPath);
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
            catch (Exception ex)
            {
                StatusText = $"載入標註失敗：{ex.Message}";
            }
        }

        private async Task SaveAnnotationsForCurrentImageAsync()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ExpectedImagePaths.Count) return;

            string imagePath = ExpectedImagePaths[CurrentImageIndex];
            string labelPath = Path.Combine(labelsUrl, Path.GetFileNameWithoutExtension(imagePath) + ".json");

            try
            {
                Directory.CreateDirectory(labelsUrl);

                var dtos = Annotations.Select(ann => new AnnotationDto
                {
                    ClassName = ann.ClassName,
                    IsPolygon = ann.IsPolygon,
                    Points = ann.Points.Select(p => new List<double> { p.X, p.Y }).ToList()
                }).ToList();

                string json = JsonSerializer.Serialize(dtos);
                await File.WriteAllTextAsync(labelPath, json);

                StatusText = "標註已儲存";
            }
            catch (Exception ex)
            {
                StatusText = $"儲存標註失敗：{ex.Message}";
            }
        }

        #endregion

        #region 滑鼠事件
        public void SetControls(Image? image, Canvas? canvas)
        {
            _imageControl = image;
            _canvas = canvas;
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
            if (IsPolygonMode && _currentPolygonPoints.Count >= 3)
            {
                _currentPolygonPoints.Add(imagePixelPos);
                FinishCurrentPolygon();
            }
            else if (!IsPolygonMode)
            {
                _isDragging = false;
            }
        }

        public void OnPointerMoved(Point imagePixelPos)
        {
            // 更新滑鼠座標顯示
            MousePositionText = $"X: {imagePixelPos.X:F1}   Y: {imagePixelPos.Y:F1}";

            if (!IsPolygonMode && _isDragging)
            {
                _currentRectEnd = imagePixelPos;
            }
            else if (IsPolygonMode && _currentPolygonPoints.Count > 0)
            {
                _tempMovePoint = imagePixelPos;
            }

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
            RedrawAllAnnotations();
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

            RedrawAllAnnotations();
        }
        #endregion 

    }
}
