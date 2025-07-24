using DeltaUpdater.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
