using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserKeyDistributionDto
    {
        public string pairingId { get; set; }
        public object encryptedKeyShard { get; set; }
    }

}