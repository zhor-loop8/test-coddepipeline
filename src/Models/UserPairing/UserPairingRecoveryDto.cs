using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models.UserPairing
{
    //object used by the request to initiate recovery to all contacts in Items
    public class UserPairingRecoveryDto
    { 
        public UserPairingRecoveryItemData[] Items { get; set; }
        public UserPairingRecoveryContact Sender { get; set; }
    }

    //object used by the request to initiate recovery to a single contact as Item
    public class UserRecoveryRequestDto
    {
        public UserPairingRecoveryItemData Item { get; set; }
        public UserPairingRecoveryContact Sender { get; set; }
    }

    public class UserPairingRecoveryItemData
    {
        public object KeyData { get; set; }
        public string PairingId { get; set; }
    }

    public class UserPairingRecoveryContact
    {
        public string name { get; set; }
        public string phoneNumber { get; set; }
    }

}
