using Amazon.Runtime.Internal.Util;
using Amazon.SimpleEmail.Model;
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
    [Route("api/user-pairing")]
    public class UserPairingController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<MessageController> _logger;
        private readonly l8p8Context _l8p8DbContext;
        private readonly PhoneService _phoneService;
        private readonly MessageService _messageService;
        private readonly FirebaseService _firebaseService;
        private readonly EventService _eventService;
        private readonly UserPairingService _userPairingService;


        private readonly Type _statusType = typeof(UserPairingStatus);

        public UserPairingController(IDistributedCache cache, ILogger<MessageController> logger,
            l8p8Context l8p8DbContext, PhoneService phoneService, MessageService messageService, FirebaseService firebaseService, EventService eventService, UserPairingService userPairingService)
        {
            _cache = cache;
            _logger = logger;
            _l8p8DbContext = l8p8DbContext;
            _phoneService = phoneService;
            _messageService = messageService;
            _firebaseService = firebaseService;
            _eventService = eventService;
            _userPairingService = userPairingService;
        }

        [HttpPost]
        [Route("invite")]
        //called by the main phone to send an invite request to a contact
        public async Task<IActionResult> Post([FromBody] UserPairingInvitationDto pairingInvitation)
        {
            if (pairingInvitation == null ||
                pairingInvitation.Recipient == null || String.IsNullOrWhiteSpace(pairingInvitation.Recipient.phoneNumber) ||
                pairingInvitation.Sender == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var res = await _userPairingService.InviteAsync(pairingInvitation, (long)client.UserId);


            if (!string.IsNullOrEmpty(res))
            {
                return Ok(new { pairingId = res });
            }
            else
            {
                return BadRequest();
            }



        }

        //[HttpPost]
        //[Route("invite")]
        ////called by the main phone to send an invite request to a contact
        //public async Task<IActionResult> Post([FromBody] UserPairingInvitationDto pairingInvitation)
        //{
        //    if (pairingInvitation == null ||
        //        pairingInvitation.Recipient == null || String.IsNullOrWhiteSpace(pairingInvitation.Recipient.phoneNumber) ||
        //        pairingInvitation.Sender == null)
        //        return BadRequest();

        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();



        //    //check to see if the contact invited already has a registered user
        //    var invitedUser = _l8p8DbContext.Users.Where(u => u.PhoneNumber == pairingInvitation.Recipient.phoneNumber).OrderByDescending(u => u.Id).FirstOrDefault();

        //    UserPairing pairing = null;

        //    if (invitedUser != null)
        //    {
        //        //check if there is an existing pairing between the main phone and the contact
        //        pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingUserId == invitedUser.Id && p.PairingUserPhoneNumber == pairingInvitation.Recipient.phoneNumber).FirstOrDefault();
        //        if (pairing != null)
        //        {
        //            pairing.PairingStatus = (int)UserPairingStatus.Pending;
        //            pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
        //            pairing.UpdateDate = DateTime.UtcNow;
        //        }
        //    }

        //    if (pairing == null)
        //    {
        //        pairing = new UserPairing();
        //        pairing.UserId = client.UserId;
        //        pairing.PairingUserPhoneNumber = pairingInvitation.Recipient.phoneNumber;
        //        pairing.PairingToken = Guid.NewGuid().ToString();
        //        pairing.PairingStatus = (int)UserPairingStatus.Pending;
        //        pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
        //        pairing.InsertDate = DateTime.UtcNow;
        //        pairing.UpdateDate = DateTime.UtcNow;

        //        if (invitedUser != null)
        //        {
        //            pairing.PairingUserId = invitedUser.Id;
        //        }

        //        _l8p8DbContext.UserPairings.Add(pairing);
        //    }
        //    _l8p8DbContext.SaveChanges();

        //    //create a pairing event
        //    var evt = new EventDto();
        //    evt.Id = Guid.NewGuid().ToString();
        //    evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { request = new { keyData = pairingInvitation.KeyData, sender = pairingInvitation.Sender } } } });

        //    //save the pub keys in redis
        //    await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

        //    //if the recovery contact has an account, send a push to the recovery contact
        //    if (invitedUser != null && invitedUser.PhoneVerified == true && invitedUser.EmailVerified == true)
        //    {
        //        var pairingEventIdsJson = await _cache.GetStringAsync(invitedUser.Id.ToString() + "_pairing");
        //        if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
        //        {
        //            //there are pending requests.  add the new request to the list
        //            var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
        //            pairingEventIds.Add(evt.Id);
        //            await _cache.SetStringAsync(invitedUser.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //        }
        //        else
        //        {
        //            List<string> pairingEventIds = new List<string>();
        //            pairingEventIds.Add(evt.Id);
        //            await _cache.SetStringAsync(invitedUser.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //        }

        //        //send the push notifiction to the invited user
        //        //add a try/catch so the function completes in case 
        //        try
        //        {
        //            var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == invitedUser.Id && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //            var invitedUserMessage = String.IsNullOrEmpty(pairingInvitation.Sender.name) ? "Someone" : pairingInvitation.Sender.name + " wants to add you as their trusted contact.";
        //            _firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Contact Invite", invitedUserMessage);
        //        }
        //        catch { }
        //    }
        //    //the invited user doesn't have an existing account
        //    //there is code in the AccountController.VerifyAccount function to associate the event to the user when the invited user registers
        //    else
        //    {
        //        pairing.InvitationEventId = evt.Id;
        //        _l8p8DbContext.SaveChanges();
        //    }

        //    return Ok(new { pairingId = pairing.PairingToken });
        //}

        [HttpPost]
        [Route("{pairingId}/invite")]
        //called by the main phone to re-send an invite request to a contact
        public async Task<IActionResult> PostResendInvite([FromRoute] string pairingId, [FromBody] UserRePairingInvitationDto rePairingInvitation)
        {
            if (rePairingInvitation == null ||
                pairingId == null ||
                rePairingInvitation.Sender == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //get the pairing
            var pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == pairingId).FirstOrDefault();
            if (pairing == null)
                return BadRequest();

            pairing.PairingStatus = (int)UserPairingStatus.Pending;
            pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
            pairing.UpdateDate = DateTime.UtcNow;
            _l8p8DbContext.SaveChanges();

            //check to see if the contact invited already has a registered user
            var invitedUser = _l8p8DbContext.Users.Where(u => u.Id == pairing.PairingUserId).OrderByDescending(u => u.Id).FirstOrDefault();
            if (invitedUser == null) //is this possible? what if the contact deleted their account?
                return BadRequest();

            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { request = new { keyData = rePairingInvitation.KeyData, sender = rePairingInvitation.Sender } } } });

            //save the pub keys in redis
            await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

            //if the recovery contact has an account, send a push to the recovery contact
            //there is code in the AccountController.VerifyAccount function to associate the event to the user when the invited user registers
            if (invitedUser != null && invitedUser.PhoneVerified == true && invitedUser.EmailVerified == true)
            {
                Event ev = new Event()
                {
                    UserId = invitedUser.Id,
                    TypeId = EventTypeEnum.Pairing,
                    EventId = new Guid(evt.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev);
                _l8p8DbContext.SaveChanges();
                //var pairingEventIdsJson = await _cache.GetStringAsync(invitedUser.Id.ToString() + "_pairing");
                //if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
                //{
                //    //there are pending requests.  add the new request to the list
                //    var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
                //    pairingEventIds.Add(evt.Id);
                //    await _cache.SetStringAsync(invitedUser.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}
                //else
                //{
                //    List<string> pairingEventIds = new List<string>();
                //    pairingEventIds.Add(evt.Id);
                //    await _cache.SetStringAsync(invitedUser.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}

                //var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == invitedUser.Id && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
                //var invitedUserMessage = String.IsNullOrEmpty(rePairingInvitation.Sender.Name) ? "Someone" : rePairingInvitation.Sender.Name + " wants to add you as their trusted contact.";
                //_firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Contact Invite", invitedUserMessage);
            }

            return Ok(new { pairingId = pairing.PairingToken });
        }



        //[HttpPost]
        //[Route("{pairingId}/invite")]
        ////called by the main phone to re-send an invite request to a contact
        //public async Task<IActionResult> PostResendInvite([FromRoute] string pairingId, [FromBody] UserRePairingInvitationDto rePairingInvitation)
        //{
        //    if (rePairingInvitation == null ||
        //        pairingId == null ||
        //        rePairingInvitation.Sender == null)
        //        return BadRequest();

        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();

        //    //get the pairing
        //    var pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == pairingId).FirstOrDefault();
        //    if (pairing == null)
        //        return BadRequest();

        //    pairing.PairingStatus = (int)UserPairingStatus.Pending;
        //    pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
        //    pairing.UpdateDate = DateTime.UtcNow;
        //    _l8p8DbContext.SaveChanges();

        //    //check to see if the contact invited already has a registered user
        //    var invitedUser = _l8p8DbContext.Users.Where(u => u.Id == pairing.PairingUserId).OrderByDescending(u => u.Id).FirstOrDefault();
        //    if (invitedUser == null) //is this possible? what if the contact deleted their account?
        //        return BadRequest();

        //    var evt = new EventDto();
        //    evt.Id = Guid.NewGuid().ToString();
        //    evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { request = new { keyData = rePairingInvitation.KeyData, sender = rePairingInvitation.Sender } } } });

        //    //save the pub keys in redis
        //    await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

        //    //if the recovery contact has an account, send a push to the recovery contact
        //    //there is code in the AccountController.VerifyAccount function to associate the event to the user when the invited user registers
        //    if (invitedUser != null && invitedUser.PhoneVerified == true && invitedUser.EmailVerified == true)
        //    {
        //        var pairingEventIdsJson = await _cache.GetStringAsync(invitedUser.Id.ToString() + "_pairing");
        //        if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
        //        {
        //            //there are pending requests.  add the new request to the list
        //            var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
        //            pairingEventIds.Add(evt.Id);
        //            await _cache.SetStringAsync(invitedUser.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //        }
        //        else
        //        {
        //            List<string> pairingEventIds = new List<string>();
        //            pairingEventIds.Add(evt.Id);
        //            await _cache.SetStringAsync(invitedUser.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //        }

        //        //var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == invitedUser.Id && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //        //var invitedUserMessage = String.IsNullOrEmpty(rePairingInvitation.Sender.Name) ? "Someone" : rePairingInvitation.Sender.Name + " wants to add you as their trusted contact.";
        //        //_firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Contact Invite", invitedUserMessage);
        //    }

        //    return Ok(new { pairingId = pairing.PairingToken });
        //}

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var pairings = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId).Select(p => new { phoneNumber = p.PairingUserPhoneNumber, pairingId = p.PairingToken, status = Enum.GetName(_statusType, p.PairingStatus).ToLower() }); ;

            return Ok(pairings);
        }

        [HttpPost]
        [Route("{pairingId}/response")]
        //called by the recovery phone to accept/reject an invite
        public async Task<IActionResult> Post([FromRoute] string pairingId, [FromBody] object json)
        {
            if (String.IsNullOrWhiteSpace(pairingId) || json == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var pairing = _l8p8DbContext.UserPairings.Where(p => (p.UserId == client.UserId || p.PairingUserId == client.UserId) && p.PairingToken == pairingId).SingleOrDefault();
            if (pairing == null)
                return BadRequest();

            if (pairing.PairingStatus == (int)UserPairingStatus.Invalid || pairing.PairingStatus == (int)UserPairingStatus.Declined || pairing.PairingStatus == (int)UserPairingStatus.Expired)
            {
                pairing.PairingStatus = (int)UserPairingStatus.Pending;
                _l8p8DbContext.SaveChanges();
            }

            //save an event for the main phone to retrieve
            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { response = json } } });

            await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

            Event ev = new Event()
            {
                UserId = (long)pairing.UserId,
                TypeId = EventTypeEnum.Pairing,
                EventId = new Guid(evt.Id),
                ExpirationDate = DateTime.UtcNow.AddDays(14),
                Acknowledged = false,
                InsertDate = DateTime.UtcNow,
            };
            _l8p8DbContext.Add(ev);
            _l8p8DbContext.SaveChanges();

            //var pairingEventIdsJson = await _cache.GetStringAsync(pairing.UserId.ToString() + "_pairing");
            //if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
            //{
            //    //there are pending requests.  add the new request to the list
            //    var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
            //    pairingEventIds.Add(evt.Id);
            //    await _cache.SetStringAsync(pairing.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
            //}
            //else
            //{
            //    List<string> pairingEventIds = new List<string>();
            //    pairingEventIds.Add(evt.Id);
            //    await _cache.SetStringAsync(pairing.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
            //}

            return Ok();
        }


        //[HttpPost]
        //[Route("{pairingId}/response")]
        ////called by the recovery phone to accept/reject an invite
        //public async Task<IActionResult> Post([FromRoute] string pairingId, [FromBody] object json)
        //{
        //    if (String.IsNullOrWhiteSpace(pairingId) || json == null)
        //        return BadRequest();

        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();

        //    var pairing = _l8p8DbContext.UserPairings.Where(p => (p.UserId == client.UserId || p.PairingUserId == client.UserId) && p.PairingToken == pairingId).SingleOrDefault();
        //    if (pairing == null)
        //        return BadRequest();

        //    if (pairing.PairingStatus == (int)UserPairingStatus.Invalid || pairing.PairingStatus == (int)UserPairingStatus.Declined || pairing.PairingStatus == (int)UserPairingStatus.Expired)
        //    {
        //        pairing.PairingStatus = (int)UserPairingStatus.Pending;
        //        _l8p8DbContext.SaveChanges();
        //    }

        //    //save an event for the main phone to retrieve
        //    var evt = new EventDto();
        //    evt.Id = Guid.NewGuid().ToString();
        //    evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { response = json } } });

        //    await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });



        //    var pairingEventIdsJson = await _cache.GetStringAsync(pairing.UserId.ToString() + "_pairing");
        //    if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
        //    {
        //        //there are pending requests.  add the new request to the list
        //        var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
        //        pairingEventIds.Add(evt.Id);
        //        await _cache.SetStringAsync(pairing.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //    }
        //    else
        //    {
        //        List<string> pairingEventIds = new List<string>();
        //        pairingEventIds.Add(evt.Id);
        //        await _cache.SetStringAsync(pairing.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        //    }

        //    return Ok();
        //}

        //[HttpPost]
        //[Route("{pairingId}/message")]
        ////used to send messages between phones after they have been paired
        ////TODO: remove as it's being replaced with /user-pairing/message endpoint
        //public async Task<IActionResult> PostMessage([FromRoute] string pairingId, [FromBody] object json)
        //{
        //    if (String.IsNullOrWhiteSpace(pairingId) || json == null)
        //        return BadRequest();

        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();

        //    var pairing = _l8p8DbContext.UserPairings.Where(p => (p.UserId == client.UserId || p.PairingUserId == client.UserId) && p.PairingToken == pairingId).SingleOrDefault();
        //    if (pairing == null)
        //        return BadRequest();

        //    if (pairing.PairingStatus != (int)UserPairingStatus.Accepted)
        //        return BadRequest();

        //    string messagingToken = null;

        //    if (pairing.UserId == client.UserId)
        //    {
        //        messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //    }
        //    else
        //    {
        //        messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //    }

        //    //save an event for the phone to retrieve
        //    var evt = new EventDto();
        //    evt.Id = Guid.NewGuid().ToString();
        //    evt.Type = JsonSerializer.Serialize(new { userMessage = new { messageData = json } });

        //    //set the event for the phone
        //    await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

        //    Event ev = new Event()
        //    {
        //        UserId = pairing.UserId == client.UserId ? (long)pairing.PairingUserId : (long)pairing.UserId,
        //        TypeId = EventTypeEnum.UserMessage,
        //        EventId = new Guid(evt.Id),
        //        ExpirationDate = DateTime.UtcNow.AddDays(14),
        //        Acknowledged = false,
        //        InsertDate = DateTime.UtcNow,
        //    };
        //    _l8p8DbContext.Add(ev);
        //    _l8p8DbContext.SaveChanges();

        //    var messageId = _firebaseService.SendBackgroundPushNotification(Guid.NewGuid().ToString(), messagingToken);

        //    return Ok();
        //}

        [HttpDelete]
        [Route("{pairingId}")]
        public async Task<IActionResult> DeletePairing([FromRoute] string pairingId)
        {
            if (String.IsNullOrWhiteSpace(pairingId))
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == pairingId).SingleOrDefault();
            if (pairing == null)
                return BadRequest();

            _l8p8DbContext.UserPairings.Remove(pairing);
            _l8p8DbContext.SaveChanges();

            //TO DO: remove the event from the cache
            //await _cache.RemoveAsync(client.UserId.ToString());

            return Ok();
        }

        [HttpPost]
        [Route("{pairingId}/status")]
        public async Task<IActionResult> PostPairingStatus([FromRoute] string pairingId, [FromBody] UserPairingStatusDto status)
        {
            if (String.IsNullOrWhiteSpace(pairingId))
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //status can be updated by either the main phone or the recovery contact
            var pairing = _l8p8DbContext.UserPairings.Where(p => (p.UserId == client.UserId || p.PairingUserId == client.UserId) && p.PairingToken == pairingId).SingleOrDefault();
            if (pairing == null)
                return BadRequest();

            switch (status.Status.ToLower())
            {
                case "pending":
                    pairing.PairingStatus = (int)UserPairingStatus.Pending;
                    break;
                case "accepted":
                    pairing.PairingStatus = (int)UserPairingStatus.Accepted;
                    break;
                case "declined":
                    pairing.PairingStatus = (int)UserPairingStatus.Declined;
                    break;
                default:
                    throw new ApplicationException($"{status.Status} is not supported.");
            }

            pairing.UpdateDate = DateTime.UtcNow;

            _l8p8DbContext.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("recovery")]
        //called by the main phone to send an invite request to a contact
        public async Task<IActionResult> PostRecovery([FromBody] UserPairingRecoveryDto req)
        {
            if (req == null ||
                req.Items == null ||
                req.Sender == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //TO DO: clear out all existing events

            //loop through the contacts
            foreach (var recoveryContact in req.Items)
            {
                UserPairing pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == recoveryContact.PairingId).FirstOrDefault();
                if (pairing == null)
                    return BadRequest();

                pairing.PairingStatus = (int)UserPairingStatus.Pending;
                pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
                pairing.UpdateDate = DateTime.UtcNow;

                _l8p8DbContext.SaveChanges();

                //save the pairing event
                var pairingEvent = new EventDto();
                pairingEvent.Id = Guid.NewGuid().ToString();
                pairingEvent.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { request = new { keyData = recoveryContact.KeyData, sender = req.Sender } } } });

                await _cache.SetStringAsync(pairingEvent.Id, JsonSerializer.Serialize(pairingEvent), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                Event ev_pairing = new Event()
                {
                    UserId = (long)pairing.PairingUserId,
                    TypeId = EventTypeEnum.Pairing,
                    EventId = new Guid(pairingEvent.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev_pairing);
                _l8p8DbContext.SaveChanges();


                //save the key request event
                var keyRequestEvent = new EventDto();
                keyRequestEvent.Id = Guid.NewGuid().ToString();
                keyRequestEvent.Type = JsonSerializer.Serialize(new { recoveryKeyShard = new { action = new { requestKey = new { pairingId = pairing.PairingToken, sender = req.Sender } } } });

                await _cache.SetStringAsync(keyRequestEvent.Id, JsonSerializer.Serialize(keyRequestEvent), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                Event ev_key_req = new Event()
                {
                    UserId = (long)pairing.PairingUserId,
                    TypeId = EventTypeEnum.KeyReq,
                    EventId = new Guid(keyRequestEvent.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev_key_req);
                _l8p8DbContext.SaveChanges();


                //send a push notification to the recovery contact
                //put a try/catch around this.  If there is an exception sending to one phone, the function will continue
                try
                {
                    var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
                    var invitedUserMessage = String.IsNullOrEmpty(req.Sender.name) ? "Someone" : req.Sender.name + " has requested account recovery";
                    _firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Contact Recovery", invitedUserMessage);
                }
                catch { }

            }

            return Ok();
        }

        [HttpGet]
        [Route("trustors")]
        public async Task<IActionResult> GetTrustors()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //filter to only accepted
            var trustorPairings = _l8p8DbContext.UserPairings.Include(p => p.User)
                .Where(p => p.PairingUserId == client.UserId && p.PairingStatus == (int)UserPairingStatus.Accepted).Select(p => new { pairingId = p.PairingToken, phoneNumber = p.User.PhoneNumber, status = Enum.GetName(_statusType, p.PairingStatus).ToLower() });

            return Ok(trustorPairings);
        }


        [HttpPost]
        [Route("reconnect/trustors")]
        //called by the trustee to reconenct with a trustor
        public async Task<IActionResult> PostReconnectTrustors([FromBody] UserPairingReconnectTrustorDto req)
        {
            if (req == null ||
                req.Items == null ||
                req.Sender == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            //loop through the items
            foreach (var trustor in req.Items)
            {
                UserPairing pairing = _l8p8DbContext.UserPairings.Where(p => p.PairingUserId == client.UserId && p.PairingToken == trustor.PairingId).FirstOrDefault();
                if (pairing == null)
                    continue;

                pairing.PairingStatus = (int)UserPairingStatus.Pending;
                //pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
                pairing.UpdateDate = DateTime.UtcNow;

                _l8p8DbContext.SaveChanges();

                //save the pairing event
                var pairingEvent = new EventDto();
                pairingEvent.Id = Guid.NewGuid().ToString();
                pairingEvent.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { reconnectTrustor = new { keyData = trustor.KeyData, sender = req.Sender } } } });

                await _cache.SetStringAsync(pairingEvent.Id, JsonSerializer.Serialize(pairingEvent), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                Event ev = new Event()
                {
                    UserId = (long)pairing.UserId,
                    TypeId = EventTypeEnum.Pairing,
                    EventId = new Guid(pairingEvent.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev);
                _l8p8DbContext.SaveChanges();

                //var pairingEventIdsJson = await _cache.GetStringAsync(pairing.UserId.ToString() + "_pairing");
                //if (!String.IsNullOrWhiteSpace(pairingEventIdsJson))
                //{
                //    //there are pending requests.  add the new request to the list
                //    var pairingEventIds = JsonSerializer.Deserialize<List<string>>(pairingEventIdsJson);
                //    pairingEventIds.Add(pairingEvent.Id);
                //    await _cache.SetStringAsync(pairing.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}
                //else
                //{
                //    List<string> pairingEventIds = new List<string>();
                //    pairingEventIds.Add(pairingEvent.Id);
                //    await _cache.SetStringAsync(pairing.UserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
                //}

            }

            return Ok();
        }

        [HttpPost]
        [Route("reconnect/trustor/response")]
        //called by the trustor to respond to a reconnect request from a trustee
        public async Task<IActionResult> PostRconnectTrustorsResponse([FromBody] UserPairingReconnectTrustorResponseDto req)
        {
            if (req == null ||
                req.KeyData == null ||
                req.PairingId == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == req.PairingId).SingleOrDefault();
            if (pairing == null)
                return BadRequest();

            //save an event for the recovery phone to retrieve
            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { reconnectTrustorResponse = new { keyData = req.KeyData } } } });

            await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

            Event ev = new Event()
            {
                UserId = (long)pairing.PairingUserId,
                TypeId = EventTypeEnum.Pairing,
                EventId = new Guid(evt.Id),
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
            //    pairingEventIds.Add(evt.Id);
            //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
            //}
            //else
            //{
            //    List<string> pairingEventIds = new List<string>();
            //    pairingEventIds.Add(evt.Id);
            //    await _cache.SetStringAsync(pairing.PairingUserId.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
            //}

            return Ok();
        }



        [HttpPost]
        [Route("message")]
        //used for sending message(s) from one user to another
        public async Task<IActionResult> PostMessage([FromBody] UserPairingMessagePayloadListDto messagePayloadListDto)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (messagePayloadListDto == null || messagePayloadListDto.Payloads == null || messagePayloadListDto.Payloads.Count == 0)
                return BadRequest();

            int recipients = await _userPairingService.SendMessage(client.UserId, messagePayloadListDto);

            return recipients > 0 ? Ok(recipients) : BadRequest();
        }
    }
}
