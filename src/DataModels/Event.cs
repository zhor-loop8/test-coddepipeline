using System;
using WebAPI.Models;

namespace WebAPI.DataModels
{
    public class Event
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public EventTypeEnum TypeId { get; set; }
        public Guid EventId { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool Acknowledged { get; set; }
        public DateTime? AcknowledgedDate { get; set; }
        public DateTime InsertDate { get; set; }
        public string FirebaseResult { get; set; }
    }
}
