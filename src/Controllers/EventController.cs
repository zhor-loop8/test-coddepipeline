using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/events")]
    public class EventController : ControllerBase
    {
        private readonly ILogger<MessageController> _logger;
        private readonly IDistributedCache _cache;
        private readonly l8p8Context _l8p8DbContext;
        private readonly FirebaseService _firebaseService;
        private readonly EventService _service;

        public EventController(ILogger<MessageController> logger, IDistributedCache cache, l8p8Context l8p8DbContext, FirebaseService firebaseService, EventService eventService)
        {
            _logger = logger;
            _cache = cache;
            _l8p8DbContext = l8p8DbContext;
            _firebaseService = firebaseService;
            _service = eventService;
        }

        //[HttpGet]
        //public async Task<IActionResult> Get()
        //{
        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();

        //    var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //    if (String.IsNullOrWhiteSpace(messagingToken))
        //        return Unauthorized();

        //    var res = new List<EventDto>();

        //    //pull the pending messages from clients
        //    var clientEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString());
        //    if (!String.IsNullOrWhiteSpace(clientEventIdsJson))
        //    {
        //        var clientEventIds = JsonSerializer.Deserialize<List<string>>(clientEventIdsJson);
                
        //        foreach (var clientEventId in clientEventIds)
        //        {
        //            var value = await _cache.GetStringAsync(clientEventId);
        //            if (!String.IsNullOrWhiteSpace(value))
        //            {
        //                var evt = JsonSerializer.Deserialize<EventDto>(value);
        //                res.Add(evt);
        //            }
        //        }
        //    }

        //    //pull the pending messages from contacts
        //    var pairingEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString()+"_pairing");
        //    if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
        //    {
        //        var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);

        //        foreach (var pairingEventId in pairingEventIds)
        //        {
        //            var value = await _cache.GetStringAsync(pairingEventId);
        //            if (!String.IsNullOrWhiteSpace(value))
        //            {
        //                var evt = JsonSerializer.Deserialize<EventDto>(value);
        //                res.Add(evt);
        //            }
        //        }
        //    }

        //    //pull the pending key distribution events
        //    var keyDistEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString() + "_keydist");
        //    if (!String.IsNullOrWhiteSpace(keyDistEventIdsJson))
        //    {
        //        var keyDistEventIds = JsonSerializer.Deserialize<List<string>>(keyDistEventIdsJson);

        //        foreach (var keyDistEventId in keyDistEventIds)
        //        {
        //            var value = await _cache.GetStringAsync(keyDistEventId);
        //            if (!String.IsNullOrWhiteSpace(value))
        //            {
        //                var evt = JsonSerializer.Deserialize<EventDto>(value);
        //                res.Add(evt);
        //            }
        //        }
        //    }

        //    //pull the pending key request events
        //    var keyReqEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString() + "_key_req");
        //    if (!String.IsNullOrWhiteSpace(keyReqEventIdsJson))
        //    {
        //        var keyReqEventIds = JsonSerializer.Deserialize<List<string>>(keyReqEventIdsJson);

        //        foreach (var keyReqEventId in keyReqEventIds)
        //        {
        //            var value = await _cache.GetStringAsync(keyReqEventId);
        //            if (!String.IsNullOrWhiteSpace(value))
        //            {
        //                var evt = JsonSerializer.Deserialize<EventDto>(value);
        //                res.Add(evt);
        //            }
        //        }
        //    }

        //    //pull the pending key request reply events
        //    var keyReqReplyEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString() + "_key_req_reply");
        //    if (!String.IsNullOrWhiteSpace(keyReqReplyEventIdsJson))
        //    {
        //        var keyReqReplyEventIds = JsonSerializer.Deserialize<List<string>>(keyReqReplyEventIdsJson);

        //        foreach (var keyReqReplyEventId in keyReqReplyEventIds)
        //        {
        //            var value = await _cache.GetStringAsync(keyReqReplyEventId);
        //            if (!String.IsNullOrWhiteSpace(value))
        //            {
        //                var evt = JsonSerializer.Deserialize<EventDto>(value);
        //                res.Add(evt);
        //            }
        //        }
        //    }

        //    return Ok(res);
        //}


        //[HttpPost]
        //[Route("acknowledge")]
        //public async Task<IActionResult> Acknowledge(EventsAcknowledgeRequest req)
        //{
        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();

        //    var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //    if (String.IsNullOrWhiteSpace(messagingToken))
        //        return Unauthorized();

        //    //get all of the event ids
        //    var clientEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString());
        //    List<string> clientEventIds = new List<string>();
        //    if (!String.IsNullOrEmpty(clientEventIdsJson))
        //        clientEventIds = JsonSerializer.Deserialize<List<string>>(clientEventIdsJson);

        //    var pairingEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString()+ "_pairing");
        //    List<string> pairingEventIds = new List<string>();
        //    if (!String.IsNullOrEmpty(pairingEventIdsJson))
        //        pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);

        //    var keyDistEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString() + "_keydist");
        //    List<string> keyDistEventIds = new List<string>();
        //    if (!String.IsNullOrEmpty(keyDistEventIdsJson))
        //        keyDistEventIds = JsonSerializer.Deserialize<List<string>>(keyDistEventIdsJson);

        //    var keyReqEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString() + "_key_req");
        //    List<string> keyReqEventIds = new List<string>();
        //    if (!String.IsNullOrEmpty(keyReqEventIdsJson))
        //        keyReqEventIds = JsonSerializer.Deserialize<List<string>>(keyReqEventIdsJson);

        //    var keyReqReplyEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString() + "_key_req_reply");
        //    List<string> keyReqReplyEventIds = new List<string>();
        //    if (!String.IsNullOrEmpty(keyReqReplyEventIdsJson))
        //        keyReqReplyEventIds = JsonSerializer.Deserialize<List<string>>(keyReqReplyEventIdsJson);

        //    //remove the processed events
        //    foreach (var processedEventId in req.eventIds)
        //    {
        //        clientEventIds.Remove(processedEventId);
        //        pairingEventIds.Remove(processedEventId);
        //        keyDistEventIds.Remove(processedEventId);
        //        keyReqEventIds.Remove(processedEventId);
        //        keyReqReplyEventIds.Remove(processedEventId);

        //        await _cache.RemoveAsync(processedEventId.ToString());
        //    }

        //    await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //    await _cache.SetStringAsync(client.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //    await _cache.SetStringAsync(client.UserId.ToString() + "_keydist", JsonSerializer.Serialize(keyDistEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });
        //    await _cache.SetStringAsync(client.UserId.ToString() + "_key_req", JsonSerializer.Serialize(keyReqEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //    await _cache.SetStringAsync(client.UserId.ToString() + "_key_req_reply", JsonSerializer.Serialize(keyReqEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });


        //    return Ok();
        //}


        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
            if (String.IsNullOrWhiteSpace(messagingToken))
                return Unauthorized();

            var res = new List<EventDto>();

            //pull the pending messages from clients

            List<Guid> clientEventIds = _service.GetUserEvents(client.UserId);

            foreach (var clientEventId in clientEventIds)
            {
                var value = await _cache.GetStringAsync(clientEventId.ToString());
                Console.WriteLine(value);
                if (!String.IsNullOrWhiteSpace(value))
                {
                    var evt = JsonSerializer.Deserialize<EventDto>(value);
                    res.Add(evt);
                }
            }
            Console.WriteLine(res.Count);
            return Ok(res);
        }


        [HttpPost]
        [Route("acknowledge")]
        public async Task<IActionResult> Acknowledge(EventsAcknowledgeRequest req)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
            if (String.IsNullOrWhiteSpace(messagingToken))
                return Unauthorized();

            Console.WriteLine($"acknowledge {req.eventIds.Length}");

            bool isUpdated = _service.Acknowledge(req);

            if (isUpdated)
            {
                return Ok();
            } else
            {
                return BadRequest();
            }

        }
    }
}
