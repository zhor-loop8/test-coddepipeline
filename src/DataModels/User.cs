using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class User
    {
        public User()
        {
            Clients = new HashSet<Client>();
            UserPairingPairingUsers = new HashSet<UserPairing>();
            UserPairingUsers = new HashSet<UserPairing>();
        }

        public long Id { get; set; }
        public string Gdi { get; set; }
        public string Email { get; set; }
        public bool? EmailVerified { get; set; }
        public string PhoneNumber { get; set; }
        public bool? PhoneVerified { get; set; }
        public int? ConfidenceScore { get; set; }
        public string AccountRegistrationToken { get; set; }
        public string AccountEmailVerificationToken { get; set; }
        public string AccountPhoneVerificationToken { get; set; }
        public DateTime? VerificationExpiration { get; set; }
        public DateTime? InsertDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string RecoveryKeyShard { get; set; }
        public Guid UserUuid { get; set; }

        public virtual ICollection<Client> Clients { get; set; }
        public virtual ICollection<UserPairing> UserPairingPairingUsers { get; set; }
        public virtual ICollection<UserPairing> UserPairingUsers { get; set; }
    }
}
