using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace UpdateBuilder.Models
{
    [DataContract(Name = "UpdateInfo", Namespace = "")]
    public class UpdateInfoModel
    {
        [DataMember(Name = "Version", IsRequired = true, Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "Folder", IsRequired = true, Order = 2)]
        public FolderModel Folder { get; set; }
    }
}
