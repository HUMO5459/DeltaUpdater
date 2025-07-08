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
        public FileInfo LocalFile { get; set; }
        public FileInfo RemoteFile { get; set; }
    }
}
