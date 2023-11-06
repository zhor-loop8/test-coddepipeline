using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Amazon.Runtime.Internal.Util;
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
using WebAPI.Models.UserAccount;
using WebAPI.Models.UserPairing;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<AccountController> _logger;
        private readonly l8p8Context _l8p8DbContext;
        private readonly FirebaseService _firebaseService;
        private readonly EmailService _emailService;
        private readonly PhoneService _phoneService;

        public AccountController(IDistributedCache cache, ILogger<AccountController> logger, l8p8Context l8p8DbContext,
            FirebaseService firebaseService, EmailService emailService, PhoneService phoneService)
        {
            _cache = cache;
            _logger = logger;
            _l8p8DbContext = l8p8DbContext;
            _firebaseService = firebaseService;
            _emailService = emailService;
            _phoneService = phoneService;
        }

        [HttpPost]
        [Route("/api/user-account")]
        public async Task<IActionResult> CreateAccount([FromBody] AccountRegistrationDto accountRegistrationDto)
        {
            if (!Request.Headers.ContainsKey("X-Anty-Forgery-Token") || Request.Headers["X-Anty-Forgery-Token"] != "3c86da31-a0c0-4944-8e8d-775b9b79a7bc")
                return BadRequest();

            if (String.IsNullOrWhiteSpace(accountRegistrationDto.ClientId) || String.IsNullOrWhiteSpace(accountRegistrationDto.MessagingToken))
            {
                return BadRequest();
            }

            var userRegistration = new UserRegistration();
            userRegistration.Email = null;
            userRegistration.EmailVerified = false;
            userRegistration.PhoneNumber = null;
            userRegistration.PhoneVerified = false;
            userRegistration.AccountRegistrationToken = Guid.NewGuid().ToString();
            userRegistration.AccountEmailVerificationToken = null;
            userRegistration.AccountPhoneVerificationToken = null;
            userRegistration.AuthenticationToken = null;
            userRegistration.VerificationExpiration = DateTime.UtcNow.AddHours(24);
            userRegistration.InsertDate = DateTime.UtcNow;
            userRegistration.UpdateDate = DateTime.UtcNow;
            userRegistration.Active = true;
            userRegistration.ClientId = accountRegistrationDto.ClientId;
            userRegistration.MessagingToken = accountRegistrationDto.MessagingToken;

            _l8p8DbContext.UserRegistrations.Add(userRegistration);
            _l8p8DbContext.SaveChanges();

            return Ok(new { accountRegistrationToken = userRegistration.AccountRegistrationToken });
        }

        [HttpPost]
        [Route("/api/user-account/verify")]
        public async Task<IActionResult> VerifyAccount([FromBody] AccountVerificationDtoV3 accountVerificationDto)
        {
            if (!Request.Headers.ContainsKey("X-Anty-Forgery-Token") || Request.Headers["X-Anty-Forgery-Token"] != "3c86da31-a0c0-4944-8e8d-775b9b79a7bc")
                return BadRequest();

            if (/*String.IsNullOrWhiteSpace(accountVerificationDto.ClientId) ||*/ String.IsNullOrWhiteSpace(accountVerificationDto.AccountRegistrationToken) ||
                String.IsNullOrWhiteSpace(accountVerificationDto.VerificationToken) || String.IsNullOrWhiteSpace(accountVerificationDto.VerificationType))
            {
                return BadRequest();
            }

            var userRegistration = _l8p8DbContext.UserRegistrations
                .Where(u => /*u.ClientId == accountVerificationDto.ClientId && */u.AccountRegistrationToken == accountVerificationDto.AccountRegistrationToken && u.Active == true)
                .FirstOrDefault();
            if (userRegistration == null)
            {
                return BadRequest();
            }

            if (userRegistration.VerificationExpiration < DateTime.UtcNow)
            {
                return StatusCode(410, "AccountRegistrationToken token has expired");
            }

            if (accountVerificationDto.VerificationType == "email")
            {
                if (userRegistration.EmailVerificationExpiration < DateTime.UtcNow)
                {
                    return StatusCode(410, "Email token has expired");
                }

                if (userRegistration.AccountEmailVerificationToken == accountVerificationDto.VerificationToken)
                {
                    userRegistration.EmailVerified = true;
                }
                else
                {
                    return StatusCode(406, "Email verification failed");
                }
            }
            else if (accountVerificationDto.VerificationType == "phone")
            {
                if (userRegistration.PhoneVerificationExpiration < DateTime.UtcNow)
                {
                    return StatusCode(410, "Phone token has expired");
                }

                if (userRegistration.AccountPhoneVerificationToken == accountVerificationDto.VerificationToken)
                {
                    userRegistration.PhoneVerified = true;

                    ////check if this is a returning user and automatically send the email verification 
                    //var user = _l8p8DbContext.Users
                    //    .Where(u => /*u.Email == userRegistration.Email || */u.PhoneNumber == userRegistration.PhoneNumber)
                    //    .OrderByDescending(u => u.Id)
                    //    .FirstOrDefault();
                    //if (user != null)
                    //{
                    //    AccountEmailRegistrationDto emailReg = new AccountEmailRegistrationDto();
                    //    emailReg.EmailAddress = user.Email;
                    //    emailReg.AccountRegistrationToken = accountVerificationDto.AccountRegistrationToken;
                    //    await EmailRegistration(emailReg);

                    //    // Extract the first 2 characters, last 3 characters, and create a string with 5 '*' characters
                    //    string firstTwoChars = user.Email.Substring(0, 2);
                    //    string lastThreeChars = user.Email.Substring(user.Email.Length - 3);
                    //    string asterisks = new string('*', 5);

                    //    // Create the formatted string
                    //    string formattedEmail = $"{firstTwoChars}{asterisks}{lastThreeChars}";

                    //    return Ok(new { authData =(object)null, foundEmail = formattedEmail });
                    //}
                }
                else
                {
                    return StatusCode(406, "Phone verification failed");
                }
            }

            _l8p8DbContext.SaveChanges(); // ?

            if (userRegistration.EmailVerified == true && userRegistration.PhoneVerified == true)
            {
                userRegistration.AuthenticationToken = Guid.NewGuid().ToString();

                var user = _l8p8DbContext.Users
                    .Where(u => /*u.Email == userRegistration.Email || */u.PhoneNumber == userRegistration.PhoneNumber)
                    .Include(u => u.Clients)
                    .OrderByDescending(u => u.Id)
                    .FirstOrDefault();

                if (user == null)
                {
                    //first-time user registration

                    userRegistration.Active = false;

                    user = new User();
                    user.UserUuid = Guid.NewGuid();
                    user.Gdi = "TBD";
                    user.Email = userRegistration.Email;
                    user.EmailVerified = true;
                    user.PhoneNumber = userRegistration.PhoneNumber;
                    user.PhoneVerified = true;
                    user.ConfidenceScore = 100;
                    user.AccountRegistrationToken = userRegistration.AccountRegistrationToken;
                    user.AccountEmailVerificationToken = userRegistration.AccountEmailVerificationToken;
                    user.AccountPhoneVerificationToken = userRegistration.AccountPhoneVerificationToken;
                    user.VerificationExpiration = userRegistration.VerificationExpiration;
                    user.InsertDate = DateTime.UtcNow;
                    user.UpdateDate = DateTime.UtcNow;

                    var client = new Client();
                    client.Gdi = "TBD";
                    client.ClientId = userRegistration.ClientId;
                    client.MessagingToken = userRegistration.MessagingToken;
                    client.AuthenticationToken = userRegistration.AuthenticationToken;
                    client.DataVault = true;
                    client.ConfidenceScore = 100;
                    client.InsertDate = DateTime.UtcNow;
                    client.UpdateDate = DateTime.UtcNow;

                    user.Clients.Add(client);

                    _l8p8DbContext.Users.Add(user);
                    _l8p8DbContext.SaveChanges();

                    //retrieve the user again since the Id field gets populated with the insert action as part of the identity field
                    user = _l8p8DbContext.Users
                     .Where(u => /*u.Email == userRegistration.Email || */u.PhoneNumber == userRegistration.PhoneNumber)
                     .Include(u => u.Clients)
                     .OrderByDescending(u => u.Id)
                     .FirstOrDefault();

                    //check if there is a pending invitation for this user to be a recovery contact
                    //only necessary if this is a first time registration
                    var pendingInvitationPairings = _l8p8DbContext.UserPairings.Where(p => p.PairingUserPhoneNumber == userRegistration.PhoneNumber && p.PairingUserId == null && p.InvitationEventId != null).ToList();
                    var pairingEventIds = new List<string>();
                    foreach (var pendingInvitationPairing in pendingInvitationPairings)
                    {
                        //this may not work.  Do we have a value for the Id here?
                        pendingInvitationPairing.PairingUserId = user.Id;

                        //add the event to the user's queue
                        //the event with the pairing key should be in the cache user the token

                        Event ev = new Event()
                        {
                            UserId = (long)client.UserId,
                            TypeId = EventTypeEnum.KeyReq,
                            EventId = new Guid(pendingInvitationPairing.InvitationEventId),
                            ExpirationDate = DateTime.UtcNow.AddDays(14),
                            Acknowledged = false,
                            InsertDate = DateTime.UtcNow,
                        };
                        _l8p8DbContext.Add(ev);
                        _l8p8DbContext.SaveChanges();

                        //pairingEventIds.Add(pendingInvitationPairing.InvitationEventId);
                    }

                    _l8p8DbContext.SaveChanges();
                    //await _cache.SetStringAsync(user.Id.ToString() + "_pairing", JsonSerializer.Serialize(pairingEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });

                    return Ok(new { authData = new { authenticationToken = userRegistration.AuthenticationToken, userId = user.UserUuid }, foundEmail = (string)null });

                }
                else if (/*user.Email == userRegistration.Email && */user.PhoneNumber == userRegistration.PhoneNumber)
                {
                    //returning user

                    userRegistration.Active = false;

                    user.AccountRegistrationToken = userRegistration.AccountRegistrationToken;
                    user.AccountEmailVerificationToken = userRegistration.AccountEmailVerificationToken;
                    user.AccountPhoneVerificationToken = userRegistration.AccountPhoneVerificationToken;
                    user.VerificationExpiration = userRegistration.VerificationExpiration;
                    user.UpdateDate = DateTime.UtcNow;

                    var client = user.Clients.Where(c => c.DataVault == true).FirstOrDefault();
                    if (client != null)
                    {
                        client.ClientId = userRegistration.ClientId;
                        client.MessagingToken = userRegistration.MessagingToken;
                        client.AuthenticationToken = userRegistration.AuthenticationToken;
                        client.UpdateDate = DateTime.UtcNow;
                    }
                    else
                    {
                        client.Gdi = "TBD";
                        client.ClientId = userRegistration.ClientId;
                        client.MessagingToken = userRegistration.MessagingToken;
                        client.AuthenticationToken = userRegistration.AuthenticationToken;
                        client.DataVault = true;
                        client.ConfidenceScore = 100;
                        client.InsertDate = DateTime.UtcNow;
                        client.UpdateDate = DateTime.UtcNow;

                        user.Clients.Add(client);
                    }

                    //set the existing user pairings to invalid
                    //need to revisit this
                    _l8p8DbContext.UserPairings.Where(p => p.UserId == user.Id).ToList().ForEach(p => { p.PairingStatus = (int)UserPairingStatus.Invalid; p.RecoveryStatus = (int)UserRecoveryStatus.Invalid; });
                    _l8p8DbContext.SaveChanges();

                    //may need to check if there is a pending invitation for this user to be a recovery contact
                    //if the invited user already has an account, theh the pairingId should exist
                    //however, if the invite was sent after the invited user created an account, but before the account is verified, there may be an issue

                    return Ok(new { authData = new { authenticationToken = userRegistration.AuthenticationToken, userId = user.UserUuid }, foundEmail = (string)null });
                }
                else
                {
                    return StatusCode(409, "Verification failed");
                }
            }

            return Ok(new { authData = (object)null, foundEmail = (string)null });
        }

        [HttpPost]
        [Route("/api/user-account/email")]
        public async Task<IActionResult> EmailRegistration([FromBody] AccountEmailRegistrationDto emailRegistrationDto)
        {
            if (/*String.IsNullOrWhiteSpace(emailRegistrationDto.ClientId) ||*/ String.IsNullOrWhiteSpace(emailRegistrationDto.AccountRegistrationToken) ||
                String.IsNullOrWhiteSpace(emailRegistrationDto.EmailAddress))
            {
                return BadRequest();
            }

            var userRegistration = _l8p8DbContext.UserRegistrations
                .Where(u => /*u.ClientId == emailRegistrationDto.ClientId && */u.AccountRegistrationToken == emailRegistrationDto.AccountRegistrationToken && u.Active == true)
                .FirstOrDefault();
            if (userRegistration == null)
            {
                return BadRequest();
            }
            if (userRegistration.VerificationExpiration < DateTime.UtcNow)
            {
                return StatusCode(410, "Token expired");
            }

            //generate random 6 digit code for phone verification
            Random r = new Random();
            int randNum = r.Next(1000000);
            string sixDigitCode = randNum.ToString("D6");

            userRegistration.Email = emailRegistrationDto.EmailAddress;
            userRegistration.EmailVerified = false;
            userRegistration.EmailVerificationExpiration = DateTime.UtcNow.AddMinutes(15);
            //userRegistration.AccountEmailVerificationToken = Guid.NewGuid().ToString();
            userRegistration.AccountEmailVerificationToken = sixDigitCode;

            userRegistration.UpdateDate = DateTime.UtcNow;

            _l8p8DbContext.SaveChanges();

            //var url = _firebaseService.CreateEmailVerificationDynamicLink(userRegistration.AccountEmailVerificationToken);
            //var messageId = _emailService.SendEmailVerification(userRegistration.Email, url);
            var messageId = _emailService.SendEmailVerification(userRegistration.Email, userRegistration.AccountEmailVerificationToken);

            return Ok();
        }

        [HttpPost]
        [Route("/api/user-account/phone")]
        public async Task<IActionResult> PhoneRegistration([FromBody] AccountPhoneRegistrationDto phoneRegistrationDto)
        {
            if (/*String.IsNullOrWhiteSpace(phoneRegistrationDto.ClientId) || */String.IsNullOrWhiteSpace(phoneRegistrationDto.AccountRegistrationToken) ||
                String.IsNullOrWhiteSpace(phoneRegistrationDto.PhoneNumber))
            {
                return BadRequest();
            }

            var userRegistration = _l8p8DbContext.UserRegistrations
                .Where(u => /*u.ClientId == phoneRegistrationDto.ClientId && */u.AccountRegistrationToken == phoneRegistrationDto.AccountRegistrationToken && u.Active == true)
                .FirstOrDefault();
            if (userRegistration == null)
            {
                return BadRequest();
            }
            if (userRegistration.VerificationExpiration < DateTime.UtcNow)
            {
                return StatusCode(410, "Token expired");
            }

            //generate random 6 digit code for phone verification
            Random r = new Random();
            int randNum = r.Next(1000000);
            string sixDigitCode = randNum.ToString("D6");

            userRegistration.PhoneNumber = phoneRegistrationDto.PhoneNumber;
            userRegistration.PhoneVerified = false;
            userRegistration.PhoneVerificationExpiration = DateTime.UtcNow.AddMinutes(15);
            userRegistration.AccountPhoneVerificationToken = sixDigitCode;
            userRegistration.UpdateDate = DateTime.UtcNow;

            _l8p8DbContext.SaveChanges();

            _phoneService.SendPhoneVerification(userRegistration.PhoneNumber, userRegistration.AccountPhoneVerificationToken);

            return Ok();
        }

        [Route("/api/user-account/messaging-token")]
        [HttpPut]
        public async Task<IActionResult> Put([FromBody] MessagingTokenDto dto)
        {
            if (dto == null || dto.MessagingToken == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var mainClient = _l8p8DbContext.Clients.Where(c => c.DataVault == true && c.UserId == client.UserId).FirstOrDefault();
            if (mainClient == null)
                return Unauthorized();

            mainClient.MessagingToken = dto.MessagingToken;
            mainClient.UpdateDate = DateTime.UtcNow;

            _l8p8DbContext.SaveChanges();

            return Ok();
        }
    }
}
