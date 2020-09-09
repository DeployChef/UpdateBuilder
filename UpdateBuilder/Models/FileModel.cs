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

        public bool QuickUpdate { get; set; }

        public bool CheckHash { get; set; }

        [XmlArray("FilePatches")]
        public List<FilePatchModel> FilePatches { get; set; } = new List<FilePatchModel>();
    }

    [XmlRoot("FilePatch")]
    public class FilePatchModel
    {
        public string Name { get; set; }

        [XmlIgnore]
        public ModifyType ModifyType { get; set; } = ModifyType.NotModified;

        public long Size { get; set; }

        public string Hash { get; set; }

        public int Version { get; set; }
    }
}
