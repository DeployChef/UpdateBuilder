using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace UpdateBuilder.Models
{
    [DataContract(Name = "Folder", Namespace = "")]
    public class FolderModel
    {
        [DataMember(Name = "Name", IsRequired = true, Order = 0)]
        public string Name { get; set; }

        public string Path { get; set; }

        [DataMember(Name = "Folders", IsRequired = true, Order = 1)]
        public Folders Folders { get; set; } = new Folders();

        [DataMember(Name = "Files", IsRequired = true, Order = 2)]
        public Files Files { get; set; } = new Files();
    }

    [CollectionDataContract(Name = "Files", Namespace = "")]
    public class Files : List<FileModel>
    {

    }

    [CollectionDataContract(Name = "Folders", Namespace = "")]
    public class Folders : List<FolderModel>
    {

    }

}
