namespace DeltaUpdater.Models
{
    public class FileInfoModel
    {
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public string Checksum { get; set; }
        public DateTime LastModified { get; set; }
    }
}
