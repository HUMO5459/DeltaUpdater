using DeltaUpdater.Enums;

namespace DeltaUpdater.Models
{
    public class FileDifference
    {
        public string RelativePath { get; set; }
        public FileChangeType ChangeType { get; set; }
        public FileInfoModel LocalFile { get; set; }
        public FileInfoModel RemoteFile { get; set; }
    }
}
