using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeltaUpdater.Models
{
    public class FileManifest
    {
        public string Version { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<FileInfoModel> Files { get; set; } = new List<FileInfoModel>();
    }
}
