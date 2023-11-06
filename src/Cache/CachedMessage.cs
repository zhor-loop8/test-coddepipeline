using System.Collections.Generic;
using System;

namespace WebAPI.Data
{
    public class CachedMessage
    {
        public CachedMessage()
        {
            CustomFields = new List<string>();
        }

        public string MessageId { get; set; }
        public object Request { get; set; }
        public DateTime? RequestTimestamp { get; set; }
        public object Response { get; set; }
        public DateTime? ResponseTimestamp { get; set; }
        public string DataVersion { get; set; }

        public List<string> CustomFields { get; set; }
    }
}
