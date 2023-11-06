using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserDataRecoveryProgressDto
    {
        public string DataVersion { get; set; }
        public List<string> ApprovedPairingIds { get; set; }
    }
}