using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class UserPairing
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long? PairingUserId { get; set; }
        public string PairingUserPhoneNumber { get; set; }
        public string PairingToken { get; set; }
        public int? PairingStatus { get; set; }
        public DateTime? InsertDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string InvitationEventId { get; set; }
        public int? RecoveryStatus { get; set; }

        public virtual User PairingUser { get; set; }
        public virtual User User { get; set; }
    }
}
