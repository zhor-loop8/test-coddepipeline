using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;

namespace WebAPI.Models.UserFileBackup
{
    public class UserFolderUsage
    {
        public long Size { get; set; }
        public long Capacity { get; set; }

        public List<UserFolder> Folders { get; set; }
    }
}
