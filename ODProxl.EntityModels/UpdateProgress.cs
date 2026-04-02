namespace ODProxl.EntityModels
{
    public class UpdateProgress
    {
        public int Percentage { get; set; }
        public string StatusText { get; set; } = "";
        public string? CurrentFile { get; set; }
    }
}
