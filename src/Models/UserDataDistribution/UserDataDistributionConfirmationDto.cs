using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserDataDistributionConfirmationDto
    {
        public UserDataDistributionConfirmationPairingDto[] Items { get; set; }
    }

    public class UserDataDistributionConfirmationPairingDto
    {
        public string PairingId { get; set; }
        public string DataVersion { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}