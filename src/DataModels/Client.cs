using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class Client
    {
        public Client()
        {
            ClientPairingClients = new HashSet<ClientPairing>();
            ClientPairingPairingClients = new HashSet<ClientPairing>();
        }

        public long Id { get; set; }
        public long UserId { get; set; }
        public string Gdi { get; set; }
        public string ClientId { get; set; }
        public long? ClientTypeId { get; set; }
        public string ClientName { get; set; }
        public string MessagingToken { get; set; }
        public string AuthenticationToken { get; set; }
        public bool? DataVault { get; set; }
        public string VaultVersion { get; set; }
        public int? ConfidenceScore { get; set; }
        public string BackupFolderId { get; set; }
        public string BackupFolderName { get; set; }
        public DateTime? BackupFolderCreationDate { get; set; }
        public DateTime? InsertDate { get; set; }
        public DateTime? UpdateDate { get; set; }


        public virtual ClientType ClientType { get; set; }
        public virtual User User { get; set; }
        public virtual ICollection<ClientPairing> ClientPairingClients { get; set; }
        public virtual ICollection<ClientPairing> ClientPairingPairingClients { get; set; }
    }
}
