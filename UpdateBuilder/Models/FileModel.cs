using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace UpdateBuilder.Models
{
    [DataContract(Name = "File", Namespace = "")]
    public class FileModel
    {
        [DataMember(Name = "Name", IsRequired = true, Order = 0)]
        public string Name { get; set; }

        public string ParentName { get; set; }

        public string FullPath { get; set; }

        [DataMember(Name = "Path", IsRequired = true, Order = 1)]
        public string Path { get; set; }

        [DataMember(Name = "Size", IsRequired = true, Order = 2)]
        public long Size { get; set; }

        [DataMember(Name = "Hash", IsRequired = true, Order = 3)]
        public string Hash { get; set; }

        [DataMember(Name = "QuickUpdate", IsRequired = true, Order = 4)]
        public bool QuickUpdate { get; set; }
    }
}
