using System;
using System.Data;
using System.Runtime.CompilerServices;

namespace WebAPI.Models.UserFileBackup
{
    public class UserFolder
    {
        public string FolderId { get; set; }
        public string FolderName { get; set; }
        public DateTime FolderCreationDate { get; set; }
        public long? FolderSize { get; set; }
    }
}
