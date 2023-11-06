using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAPI.DataModels;
using WebAPI.Models;

namespace WebAPI.Services
{
    public class EventService
    {
        private readonly IConfiguration _configuration;
        private readonly l8p8Context _dbContext;

        public EventService(IConfiguration configuration, l8p8Context dbcontext)
        {
            _configuration = configuration;
            _dbContext = dbcontext;

        }


        public List<Guid> GetUserEvents(long? userId)
        {
            return _dbContext.Events.Where(e => e.UserId == userId && !e.Acknowledged && e.ExpirationDate >= DateTime.UtcNow).Select(e => e.EventId).ToList();
        }

        public bool Acknowledge(EventsAcknowledgeRequest req)
        {
            List<Event> events =  _dbContext.Events.Where(e => req.eventIds.Contains(e.EventId)).ToList();

            foreach (Event e in events) 
            {
                e.Acknowledged = true;
                e.AcknowledgedDate = DateTime.UtcNow;
            }

            _dbContext.UpdateRange(events);
            _dbContext.SaveChanges();
            return true;
        }

    }
}
