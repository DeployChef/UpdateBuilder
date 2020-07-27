using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace UpdateBuilder.Models
{
    [XmlRoot("UpdateInfo")]
    public class UpdateInfoModel
    {
        public FolderModel Folder { get; set; }
    }
}
