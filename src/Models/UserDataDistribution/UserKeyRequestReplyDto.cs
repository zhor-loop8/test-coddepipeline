using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserKeyRequestReplyDto
    {
        public string pairingId { get; set; }
        public bool accepted { get; set; }
        public object encryptedKeyShard { get; set; }
    }

}