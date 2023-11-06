using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class ClientPairing
    {
        public long Id { get; set; }
        public long? ClientId { get; set; }
        public long? PairingClientId { get; set; }
        public string PairingToken { get; set; }
        public DateTime? PairingTokenExpiration { get; set; }
        public string PairingPayload { get; set; }
        public bool? Paired { get; set; }
        public DateTime? InsertDate { get; set; }
        public DateTime? UpdateDate { get; set; }

        public virtual Client Client { get; set; }
        public virtual Client PairingClient { get; set; }
    }
}
