using Amazon.Runtime.Internal.Util;
using Amazon.SecretsManager;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models;
using WebAPI.Models.UserPairing;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/recovery-key")]
    public class UserKeyDistributionController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<MessageController> _logger;
        private readonly l8p8Context _l8p8DbContext;
        private readonly PhoneService _phoneService;
        private readonly MessageService _messageService;
        private readonly FirebaseService _firebaseService;
        private readonly EventService _eventService;
        private readonly object _lock = new object();

        private readonly Type _recoveryStatusType = typeof(UserRecoveryStatus);

        public UserKeyDistributionController(IDistributedCache cache, ILogger<MessageController> logger, l8p8Context l8p8DbContext, PhoneService phoneService, MessageService messageService, FirebaseService firebaseService, EventService eventService)
        {
            _cache = cache;
            _logger = logger;
            _l8p8DbContext = l8p8DbContext;
            _phoneService = phoneService;
            _messageService = messageService;
            _firebaseService = firebaseService;
            _eventService = eventService;
        }

        [HttpPost]
        [Route("community-shard/store")]
        public async Task<IActionResult> Post([FromBody] UserKeyDistributionDto keyDistributionDto)
        {
            if (keyDistributionDto == null || keyDistributionDto.pairingId == null || keyDistributionDto.encryptedKeyShard == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //get the recipient
            var pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == keyDistributionDto.pairingId).SingleOrDefault();
            if (pairing == null)
                return BadRequest();

            string messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();

            //save the encrypted payload in the DB
            //TO DO

            //create an event for the recipient
            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { recoveryKeyShard = new { action = new { store = new { data = keyDistributionDto } } } });

            await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });

            Event ev = new Event()
            {
                UserId = (long)pairing.PairingUserId,
                TypeId = EventTypeEnum.Keydist,
                EventId = new Guid(evt.Id),
                ExpirationDate = DateTime.UtcNow.AddDays(14),
                Acknowledged = false,
                InsertDate = DateTime.UtcNow,
            };
            _l8p8DbContext.Add(ev);
            _l8p8DbContext.SaveChanges();

            //var userKeyDistributionsJson = await _cache.GetStringAsync(pairing.PairingUserId.ToString() + "_keydist");
            //if (!String.IsNullOrWhiteSpace(userKeyDistributionsJson))
            //{
            //    //there are pending requests.  add the new request to the list
            //    //do we need to clear out possible duplicates?
            //    var userKeyDistributions = JsonSerializer.Deserialize<List<string>>(userKeyDistributionsJson);
            //    userKeyDistributions.Add(evt.Id);
            //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_keydist", JsonSerializer.Serialize(userKeyDistributions), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });
            //}
            //else
            //{
            //    List<string> userKeyDistributions = new List<string>();
            //    userKeyDistributions.Add(evt.Id);
            //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_keydist", JsonSerializer.Serialize(userKeyDistributions), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });
            //}

            return Ok();
        }


        [HttpPost]
        [Route("community-shard/request")]
        //called by the main phone to send an invite request to a contact
        public async Task<IActionResult> Post([FromBody] UserRecoveryRequestDto req)
        {
            if (req == null ||
                req.Item == null ||
                req.Sender == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //TO DO: check for existing events

            UserPairing pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == req.Item.PairingId).FirstOrDefault();
            if (pairing == null)
                return BadRequest();

            //if the paring status is pending, we can assume the original pairing event is still in the queue.  We need to handle expiration
            //should we find the existing event and update the expiration?
            if (pairing.PairingStatus != (int)UserPairingStatus.Accepted && pairing.PairingStatus != (int)UserPairingStatus.Pending)
            {
                //save the pairing event
                var pairingEvent = new EventDto();
                pairingEvent.Id = Guid.NewGuid().ToString();
                pairingEvent.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { request = new { keyData = req.Item.KeyData, sender = req.Sender } } } });

                await _cache.SetStringAsync(pairingEvent.Id, JsonSerializer.Serialize(pairingEvent), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                Event ev = new Event()
                {
                    UserId = (long)pairing.PairingUserId,
                    TypeId = EventTypeEnum.Pairing,
                    EventId = new Guid(pairingEvent.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev);
                _l8p8DbContext.SaveChanges();

                //var pairingEventIdsJson = await _cache.GetStringAsync(pairing.PairingUserId.ToString() + "_pairing");
                //if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
                //{
                //    //there are pending requests.  add the new request to the list
                //    var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
                //    pairingEventIds.Add(pairingEvent.Id);
                //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}
                //else
                //{
                //    List<string> pairingEventIds = new List<string>();
                //    pairingEventIds.Add(pairingEvent.Id);
                //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}
            }

            //if the recovery status is pending, we can assume the original pairing event is still in the queue.  We need to handle expiration
            //should we find the existing event and update the expiration?
            if (pairing.RecoveryStatus != (int)UserRecoveryStatus.Accepted && pairing.RecoveryStatus != (int)UserRecoveryStatus.Pending)
            {
                //save the key request event
                var keyRequestEvent = new EventDto();
                keyRequestEvent.Id = Guid.NewGuid().ToString();
                keyRequestEvent.Type = JsonSerializer.Serialize(new { recoveryKeyShard = new { action = new { requestKey = new { pairingId = pairing.PairingToken, sender = req.Sender } } } });

                await _cache.SetStringAsync(keyRequestEvent.Id, JsonSerializer.Serialize(keyRequestEvent), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                Event ev = new Event()
                {
                    UserId = (long)pairing.PairingUserId,
                    TypeId = EventTypeEnum.KeyReq,
                    EventId = new Guid(keyRequestEvent.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev);
                _l8p8DbContext.SaveChanges();

                //var userKeyRequestsJson = await _cache.GetStringAsync(pairing.PairingUserId.ToString() + "_key_req");
                //if (!String.IsNullOrWhiteSpace(userKeyRequestsJson))
                //{
                //    //there are pending requests.  add the new request to the list
                //    //do we need to clear out possible duplicates?
                //    var userKeyRequests = JsonSerializer.Deserialize<List<string>>(userKeyRequestsJson);
                //    userKeyRequests.Add(keyRequestEvent.Id);
                //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_key_req", JsonSerializer.Serialize(userKeyRequests), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}
                //else
                //{
                //    List<string> userKeyRequests = new List<string>();
                //    userKeyRequests.Add(keyRequestEvent.Id);
                //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_key_req", JsonSerializer.Serialize(userKeyRequests), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}
            }

            //send a push notification to the recovery contact
            //put a try/catch around this.  If there is an exception sending to one phone, the function will continue
            try
            {
                var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
                var invitedUserMessage = String.IsNullOrEmpty(req.Sender.name) ? "Someone" : req.Sender.name + " has requested account recovery";
                _firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Contact Recovery", invitedUserMessage);
            }
            catch { }
           

            return Ok();
        }

        [HttpPost]
        [Route("community-shard/request-reply")]
        //called by the recovery phone to return the key
        public async Task<IActionResult> PostRequestReply([FromBody] UserKeyRequestReplyDto keyRequestReplyDto)
        {
            if (keyRequestReplyDto == null || keyRequestReplyDto.pairingId == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            UserPairing pairing = _l8p8DbContext.UserPairings.Where(p => p.PairingUserId == client.UserId && p.PairingToken == keyRequestReplyDto.pairingId).FirstOrDefault();
            if (pairing == null)
                return BadRequest();

            if (keyRequestReplyDto.accepted)
            {
                if (keyRequestReplyDto.encryptedKeyShard == null)
                    return BadRequest();

                //save the reply in the DB
                pairing.RecoveryStatus = (int)UserRecoveryStatus.Accepted;
                _l8p8DbContext.SaveChanges();

                //check if 3 or more recovery contacts have replied
                int acceptances = _l8p8DbContext.UserPairings.Where(p => p.UserId == pairing.UserId && p.RecoveryStatus == (int)UserRecoveryStatus.Accepted).Count();
                if (acceptances == 1)
                {
                    //get the server shard
                    var serverKey = _l8p8DbContext.Users.Where(u => u.Id == pairing.UserId).Select(u => u.RecoveryKeyShard).FirstOrDefault();

                    //create event for main phone that recovery is possible
                    var evt = new EventDto();
                    evt.Id = Guid.NewGuid().ToString();
                    evt.Type = JsonSerializer.Serialize(new { vaultRecovery = new { data = new { serverShard = serverKey, encryptedCommunityShard = keyRequestReplyDto } } });

                    await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });

                    Event ev = new Event()
                    {
                        UserId = (long)pairing.UserId,
                        TypeId = EventTypeEnum.KeyReqReply,
                        EventId = new Guid(evt.Id),
                        ExpirationDate = DateTime.UtcNow.AddDays(14),
                        Acknowledged = false,
                        InsertDate = DateTime.UtcNow,
                    };
                    _l8p8DbContext.Add(ev);
                    _l8p8DbContext.SaveChanges();

                    //var userKeyRequestRepliesJson = await _cache.GetStringAsync(pairing.UserId.ToString() + "_key_req_reply");
                    //if (!String.IsNullOrWhiteSpace(userKeyRequestRepliesJson))
                    //{
                    //    //there are pending requests.  add the new request to the list
                    //    //do we need to clear out possible duplicates?
                    //    var userKeyRequestReplies = JsonSerializer.Deserialize<List<string>>(userKeyRequestRepliesJson);
                    //    userKeyRequestReplies.Add(evt.Id);
                    //    await _cache.SetStringAsync(pairing.UserId.ToString() + "_key_req_reply", JsonSerializer.Serialize(userKeyRequestReplies), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });
                    //}
                    //else
                    //{
                    //    List<string> userKeyRequestReplies = new List<string>();
                    //    userKeyRequestReplies.Add(evt.Id);
                    //    await _cache.SetStringAsync(pairing.UserId.ToString() + "_key_req_reply", JsonSerializer.Serialize(userKeyRequestReplies), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60) });
                    //}

                    //send a push to the main phone
                    var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
                    var invitedUserMessage = "Recovery has been accepted by your community";
                    _firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Recovery", invitedUserMessage);


                    //TO DO: remove the push notifications from firebase for all recovery contacts who did not respond

                    //TO DO: remove the key request events from the recovery contacts who haven't responded
                    //var recoveryContactPairings = _l8p8DbContext.UserPairings.Where(p => p.UserId == pairing.UserId).ToArray();
                    //foreach (var recoveryContactPairing in recoveryContactPairings)
                    //{
                       
                    //}

                }
            }
            else
            {
                pairing.RecoveryStatus = (int)UserRecoveryStatus.Declined;
                _l8p8DbContext.SaveChanges();
            }

            return Ok();
        }

        [HttpPost]
        [Route("server-shard")]
        public async Task<IActionResult> PostServerKey([FromBody] UserServerKeyDistributionDto serverKey)
        {
            if (serverKey == null || serverKey.KeyShard == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //get the user
            var user = _l8p8DbContext.Users.Where(u => u.Id == client.UserId).OrderByDescending(u => u.Id).FirstOrDefault();

            user.RecoveryKeyShard = serverKey.KeyShard.ToString();

            //save the encrypted payload
            _l8p8DbContext.SaveChanges();

            return Ok();
        }

        [HttpGet]
        [Route("recovery/status")]
        //called by the main phone to send an invite request to a contact
        public async Task<IActionResult> GetRecoveryStatus()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var pairings = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId).Select(p => new { phoneNumber = p.PairingUserPhoneNumber, pairingId = p.PairingToken, recoveryState = Enum.GetName(_recoveryStatusType, (p.RecoveryStatus == null ? 0:p.RecoveryStatus)).ToLower() });

            return Ok(pairings);
        }

    }
}
