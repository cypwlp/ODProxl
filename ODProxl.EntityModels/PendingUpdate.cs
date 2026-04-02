namespace ODProxl.EntityModels
{
    public class PendingUpdate
    {
        public string Version { get; set; } = string.Empty;
        public string UpdateType { get; set; } = "Full";
        public List<UpdateFile> Files { get; set; } = new();
        public string[] RestartArgs { get; set; } = Array.Empty<string>();
        public bool DeleteAfterApply { get; set; } = true;
    }
}
