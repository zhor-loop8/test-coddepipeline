namespace WebAPI.Models
{
    public class UserAccountRegistrationDto
    {
        public string EmailAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string ClientId { get; set; }
        public string MessagingToken { get; set; }
    }
}
