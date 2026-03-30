using Avalonia;

namespace ODProxl.EntityModels
{
    public class Annotation
    {
        public List<Point> Points { get; set; } = new();
        public bool IsPolygon { get; set; }
        public string ClassName { get; set; } = "";
        public string DisplayText =>
            IsPolygon
                ? $"多邊形 ({Points.Count} 點) - {ClassName}"
                : $"矩形 - {ClassName}";
    }
    public class ClassItem
    {
        public string Name { get; set; } = "";
    }

   public class AnnotationDto
    {
        public List<List<double>> Points { get; set; } = new();
        public bool IsPolygon { get; set; }
        public string ClassName { get; set; } = "";
    }
}
