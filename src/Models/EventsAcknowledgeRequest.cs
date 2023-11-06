using System;
using System.Collections.Generic;

namespace WebAPI.Models
{
    public class EventsAcknowledgeRequest
    {
        public Guid[] eventIds { get; set; }
    }
}
