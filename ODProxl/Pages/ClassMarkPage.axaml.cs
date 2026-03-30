using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ODProxl.ViewModels.Pages;
using System;

namespace ODProxl;

public partial class ClassMarkPage : UserControl
{
    public ClassMarkPage()
    {
        InitializeComponent();
    }

    private ScaleTransform? _scaleTransform;
    private ScrollViewer? _scrollViewer;
    private Border? _zoomContainer;
    private double _zoomFactor = 1.0;
    private const double ZoomStep = 0.1;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ClassMarkPageViewModel vm)
        {
            _scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");
            _zoomContainer = this.FindControl<Border>("ZoomContainer");
            if (_zoomContainer != null)
            {
                _scaleTransform = _zoomContainer.RenderTransform as ScaleTransform;
                if (_scaleTransform != null)
                {
                    _scaleTransform.ScaleX = _zoomFactor;
                    _scaleTransform.ScaleY = _zoomFactor;
                }
            }
            var image = this.FindControl<Image>("ImageViewer");
            var canvas = this.FindControl<Canvas>("DrawingCanvas");
            if (image != null && canvas != null)
            {
                vm.SetControls(image, canvas);
            }
            vm.RequestResetZoom += () =>
            {
                _zoomFactor = 1.0;
                if (_scaleTransform != null)
                {
                    _scaleTransform.ScaleX = _zoomFactor;
                    _scaleTransform.ScaleY = _zoomFactor;
                }
                if (_scrollViewer != null)
                {
                    _scrollViewer.Offset = new Vector(0, 0);
                }
                vm.ZoomLevel = _zoomFactor;
                vm.RedrawAllAnnotations();
            };
        }
    }

    private void UpdateZoom(double delta, Point? mousePos = null)
    {
        if (_scaleTransform == null || _scrollViewer == null || _zoomContainer == null) return;
        double oldZoom = _zoomFactor;
        double newZoom = _zoomFactor + delta;
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.001) return;
        _zoomFactor = newZoom;
        _scaleTransform.ScaleX = _zoomFactor;
        _scaleTransform.ScaleY = _zoomFactor;
        if (mousePos.HasValue)
        {
            var oldOffset = _scrollViewer.Offset;
            var mouseInContentOld = new Point(mousePos.Value.X / oldZoom, mousePos.Value.Y / oldZoom);
            var mouseInContentNew = new Point(mousePos.Value.X / _zoomFactor, mousePos.Value.Y / _zoomFactor);
            var deltaOffset = new Vector(
                (mouseInContentNew.X - mouseInContentOld.X) * _zoomFactor,
                (mouseInContentNew.Y - mouseInContentOld.Y) * _zoomFactor);
            _scrollViewer.Offset = new Vector(
                oldOffset.X + deltaOffset.X,
                oldOffset.Y + deltaOffset.Y);
        }
        if (DataContext is ClassMarkPageViewModel vm)
        {
            vm.ZoomLevel = _zoomFactor;
            vm.RedrawAllAnnotations();
        }
    }

    private Point GetImagePixelPosition(PointerEventArgs e)
    {
        var pos = e.GetPosition(this.FindControl<Canvas>("DrawingCanvas"));
        return pos;
    }

    private void DrawingCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var mousePos = e.GetPosition(_zoomContainer);
        double delta = e.Delta.Y > 0 ? ZoomStep : -ZoomStep;
        UpdateZoom(delta, mousePos);
        e.Handled = true;
    }

    private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ClassMarkPageViewModel vm) return;
        var pixelPos = GetImagePixelPosition(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            vm.OnPointerPressedRight(pixelPos);
        }
        else
        {
            vm.OnPointerPressed(pixelPos);
        }
    }

    private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is ClassMarkPageViewModel vm)
        {
            var pixelPos = GetImagePixelPosition(e);
            vm.OnPointerMoved(pixelPos);
        }
    }

    private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is ClassMarkPageViewModel vm)
        {
            var pixelPos = GetImagePixelPosition(e);
            vm.OnPointerReleased(pixelPos);
        }
    }

    private async void LoadFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ClassMarkPageViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "選擇PDF資料夾"
        });
        if (folders?.Count > 0)
        {
            await vm.ProcessPdfFolderAsync(folders[0].Path.LocalPath);
        }
    }

    private async void LoadFileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ClassMarkPageViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "選擇PDF文件",
            FileTypeFilter = new[] { new FilePickerFileType("PDF文件") { Patterns = new[] { "*.pdf" } } }
        });
        if (files?.Count > 0)
        {
            await vm.ProcessPdfFileAsync(files[0].Path.LocalPath);
        }
    }
}