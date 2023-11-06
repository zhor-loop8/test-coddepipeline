namespace WebAPI.Models.UserAccount
{
    public class AccountVerificationDto
    {
        //public string ClientId { get; set; }
        public string AccountRegistrationToken { get; set; }
        public string EmailVerificationToken { get; set; }
        public string PhoneVerificationToken { get; set; }
    }

    public class AccountVerificationDtoV3
    {
        //public string ClientId { get; set; }
        public string AccountRegistrationToken { get; set; }
        public string VerificationToken { get; set; }
        public string VerificationType { get; set; }
    }
}
