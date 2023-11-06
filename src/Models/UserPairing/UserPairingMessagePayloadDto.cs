using System.Collections.Generic;

namespace WebAPI.Models.UserPairing
{
    public class UserPairingMessagePayloadListDto
    {
        public List<UserPairingMessagePayloadDto> Payloads { get; set; }
    }

    public class UserPairingMessagePayloadDto
    {
        public string PairingId { get; set; }
        public object Payload { get; set; }
    }

}
