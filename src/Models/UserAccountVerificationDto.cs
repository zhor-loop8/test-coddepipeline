namespace WebAPI.Models
{
    public class UserAccountVerificationDto
    {
        public string ClientId { get; set; }
        public string VerificationToken { get; set; }
        public string VerificationType { get; set; }
    }
}
