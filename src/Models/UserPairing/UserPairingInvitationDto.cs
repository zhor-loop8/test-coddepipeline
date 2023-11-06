using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    public class UserPairingInvitationDto
    {
        public UserPairingContact Recipient { get; set; }
        public UserPairingContact Sender { get; set; }
        public string CustomMessage { get; set; }
        public object KeyData { get; set; } //includes the key data shared between phones for handshake


    }

    public class UserRePairingInvitationDto
    {
        public UserPairingContact Sender { get; set; }
        public object KeyData { get; set; } //includes the key data shared between phones for handshake

    }

    public class UserPairingContact
    {
        public string name { get; set; }
        public string phoneNumber { get; set; }
    }


}
