using Amazon.Runtime.Internal.Util;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models;
using WebAPI.Models.UserPairing;

namespace WebAPI.Services
{
    public class UserPairingService
    {
        private readonly l8p8Context _dbContext;
        private readonly IDistributedCache _cache;
        private readonly FirebaseService _firebaseService;


        public UserPairingService(IDistributedCache cache, l8p8Context dbcontext, FirebaseService firebaseService)
        {
            _cache = cache;
            _dbContext = dbcontext;
            _firebaseService = firebaseService;
        }


        public async Task<string> InviteAsync(UserPairingInvitationDto pairingInvitation, long userId)
        {

            //check to see if the contact invited already has a registered user
            var invitedUser = _dbContext.Users.Where(u => u.PhoneNumber == pairingInvitation.Recipient.phoneNumber).OrderByDescending(u => u.Id).FirstOrDefault();
            UserPairing pairing = null;

            if (invitedUser != null)
            {
                //check if there is an existing pairing between the main phone and the contact
                pairing = _dbContext.UserPairings.Where(p => p.UserId == userId && p.PairingUserId == invitedUser.Id && p.PairingUserPhoneNumber == pairingInvitation.Recipient.phoneNumber).FirstOrDefault();
                if (pairing != null)
                {
                    pairing.PairingStatus = (int)UserPairingStatus.Pending;
                    pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
                    pairing.UpdateDate = DateTime.UtcNow;
                }
            }

            if (pairing == null)
            {
                pairing = new UserPairing();
                pairing.UserId = userId;
                pairing.PairingUserPhoneNumber = pairingInvitation.Recipient.phoneNumber;
                pairing.PairingToken = Guid.NewGuid().ToString();
                pairing.PairingStatus = (int)UserPairingStatus.Pending;
                pairing.RecoveryStatus = (int)UserRecoveryStatus.Pending;
                pairing.InsertDate = DateTime.UtcNow;
                pairing.UpdateDate = DateTime.UtcNow;

                if (invitedUser != null)
                {
                    pairing.PairingUserId = invitedUser.Id;
                }

                _dbContext.UserPairings.Add(pairing);
            }
            _dbContext.SaveChanges();

            //create a pairing event
            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { userPairing = new { pairingId = pairing.PairingToken, step = new { request = new { keyData = pairingInvitation.KeyData, sender = pairingInvitation.Sender } } } });

            //save the pub keys in redis
            await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

            //if the recovery contact has an account, send a push to the recovery contact
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
                _dbContext.Add(ev);
                _dbContext.SaveChanges();

                //send the push notifiction to the invited user
                //add a try/catch so the function completes in case 
                try
                {
                    var messagingToken = _dbContext.Clients.Where(c => c.UserId == invitedUser.Id && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
                    var invitedUserMessage = String.IsNullOrEmpty(pairingInvitation.Sender.name) ? "Someone" : pairingInvitation.Sender.name + " wants to add you as their trusted contact.";
                    _firebaseService.SendPushNotification(Guid.NewGuid().ToString(), messagingToken, "Contact Invite", invitedUserMessage);
                }
                catch
                {

                }
            }
            //the invited user doesn't have an existing account
            //there is code in the AccountController.VerifyAccount function to associate the event to the user when the invited user registers
            else
            {
                pairing.InvitationEventId = evt.Id;
                _dbContext.SaveChanges();
            }

            return pairing.PairingToken;
        }


        public async Task<int> SendMessage(long userId, UserPairingMessagePayloadListDto messagePayloadListDto)
        {
            int recipients = 0;

            foreach (var payload in messagePayloadListDto.Payloads)
            {
                var pairing = _dbContext.UserPairings.Where(p => (p.UserId == userId && p.PairingUserId != null && p.PairingStatus == (int)UserPairingStatus.Accepted && p.PairingToken == payload.PairingId)).SingleOrDefault();
                if (pairing != null)
                {
                    var evt = new EventDto();
                    evt.Id = Guid.NewGuid().ToString();
                    evt.Type = JsonSerializer.Serialize(new { userMessage = new { pairingId = payload.PairingId, payload = payload.Payload } });

                    //set the event for the phone
                    await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                    Event ev = new Event()
                    {
                        UserId = (long)pairing.PairingUserId,
                        TypeId = EventTypeEnum.UserMessage,
                        EventId = new Guid(evt.Id),
                        ExpirationDate = DateTime.UtcNow.AddDays(14),
                        Acknowledged = false,
                        InsertDate = DateTime.UtcNow,
                    };

                    var messagingToken = _dbContext.Clients.Where(c => c.UserId == pairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
                    if (!String.IsNullOrWhiteSpace(messagingToken))
                    {
                        try
                        {
                            ev.FirebaseResult = _firebaseService.SendBackgroundPushNotification(evt.Id, messagingToken);
                        }
                        catch (FirebaseMessagingException fmex)
                        {
                            ev.FirebaseResult =  fmex.ErrorCode.ToString();
                        }
                        catch (Exception ex)
                        {
                            ev.FirebaseResult = ex.Message[250..];
                        }
                    }
                    else
                    {
                        ev.FirebaseResult = "FM token is missing";
                    }

                    _dbContext.Add(ev);
                    _dbContext.SaveChanges();

                    recipients++;
                }
            }

            return recipients;
        }
    }
}
