using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserDataRecoveryDto
    {
        public string PairingId { get; set; }
        public string DataVersion { get; set; }
        public string PayloadType { get; set; }
        public string Payload { get; set; }
    }

    public class UserDataRecovery
    {
        public string DataVersion { get; set; }
        public UserDataRecoveryDto[] Items { get; set; }
        public string [] Logs { get; set; }
    }
}