namespace WebAPI.Models
{
    public class ClientPairingDto
    {
        public string PairingToken { get; set; }
        public string PairingClientId { get; set; }
        public string PairingClientName { get; set; }
        public string PairingPayload { get; set; }
    }
}
