using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class UserRegistration
    {
        public long Id { get; set; }
        public string Email { get; set; }
        public bool? EmailVerified { get; set; }
        public string PhoneNumber { get; set; }
        public bool? PhoneVerified { get; set; }
        public string AuthenticationToken { get; set; }
        public string AccountRegistrationToken { get; set; }
        public string AccountEmailVerificationToken { get; set; }
        public string AccountPhoneVerificationToken { get; set; }
        public DateTime? VerificationExpiration { get; set; }
        public DateTime? EmailVerificationExpiration { get; set; }
        public DateTime? PhoneVerificationExpiration { get; set; }
        public string ClientId { get; set; }
        public string MessagingToken { get; set; }
        public bool? Active { get; set; }
        public DateTime? InsertDate { get; set; }
        public DateTime? UpdateDate { get; set; }
    }
}
