using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class ClientType
    {
        public ClientType()
        {
            Clients = new HashSet<Client>();
        }

        public long Id { get; set; }
        public string Type { get; set; }
        public string Secret { get; set; }

        public virtual ICollection<Client> Clients { get; set; }
    }
}
