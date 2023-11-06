using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserPairingStatusDto
    {
        public string Status { get; set; }
    }

    public enum UserPairingStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Deleted = 3,
        Invalid = 4,
        Expired = 5
    }

    public enum UserRecoveryStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Deleted = 3,
        Invalid = 4,
        Expired = 5
    }

}
