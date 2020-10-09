using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace UpdateBuilder.Models
{
    [XmlRoot("File")]
    public class FileModel
    {
        public string Name { get; set; }

        [XmlIgnore]
        public ModifyType ModifyType { get; set; }

        [XmlIgnore]
        public string FullPath { get; set; }

        public string Path { get; set; }

        public long Size { get; set; }

        public string Hash { get; set; }

        public bool QuickUpdate { get; set; } = true;

        public bool CheckHash { get; set; } = true;

        [XmlArray("FileUpdates")]
        public List<FileUpdateModel> FileUpdates { get; set; } = new List<FileUpdateModel>();
    }

    [XmlRoot("File")]
    public class FileUpdateModel
    {
        public string Name { get; set; }

        [XmlIgnore]
        public ModifyType ModifyType { get; set; }

        public long Size { get; set; }

        public string Hash { get; set; }

        public int Version { get; set; }
    }
}
