using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserPairingReconnectTrustorDto
    { 
        public UserPairingReconnectTrustorItemData[] Items { get; set; }
        public UserPairingReconnectTrustorContact Sender { get; set; }
    }

    public class UserPairingReconnectTrustorItemData
    {
        public object KeyData { get; set; }
        public string PairingId { get; set; }
    }

    public class UserPairingReconnectTrustorContact
    {
        public string name { get; set; }
        public string phoneNumber { get; set; }
    }

    public class UserPairingReconnectTrustorResponseDto
    {
        public object KeyData { get; set; }
        public string PairingId { get; set; }
    }

}
