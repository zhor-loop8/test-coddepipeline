using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserDataDistributionDto
    {
        public string DataVersion { get; set; }
        public UserDataDistributionPairingDto[] Items { get; set; }
    }

    public class UserDataDistributionPairingDto
    {
        public string PairingId { get; set; }
        public string PayloadType { get; set; }
        public string Payload { get; set; }
    }

    public class UserDataDistribution
    {
        public UserDataDistributionPairing[] Items { get; set; }
    }

    public class UserDataDistributionPairing
    {
        public string PairingId { get; set; }
        public string DataVersion { get; set; }
        public string PayloadType { get; set; }
        public string Payload { get; set; }
        public string Timestamp { get; set; }
    }
}