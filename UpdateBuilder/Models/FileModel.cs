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
        public string ParentName { get; set; }

        [XmlIgnore]
        public string FullPath { get; set; }

        public string Path { get; set; }

        public long Size { get; set; }

        public string Hash { get; set; }

        public bool QuickUpdate { get; set; }

        public bool CheckHash { get; set; }
    }
}
