namespace WebAPI.Models.UserAccount
{
    public class EmailRegistrationDto
    {
        public string ClientId { get; set; }
        public string EmailAddress { get; set; }
        public string MessagingToken { get; set; }
    }
}
