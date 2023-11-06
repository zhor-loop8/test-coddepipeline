using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models.UserPairing;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/backup/recovery")]
    public class UserDataRecoveryController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MessageController> _logger;
        private readonly l8p8Context _l8p8DbContext;
        private readonly PhoneService _phoneService;
        private readonly MessageService _messageService;
        private readonly FirebaseService _firebaseService;
        private readonly EventService _eventService;
        private readonly object _lock = new object();

        public UserDataRecoveryController(IMemoryCache memoryCache, ILogger<MessageController> logger, l8p8Context l8p8DbContext, PhoneService phoneService, MessageService messageService, FirebaseService firebaseService, EventService eventService)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _l8p8DbContext = l8p8DbContext;
            _phoneService = phoneService;
            _messageService = messageService;
            _firebaseService = firebaseService;
            _eventService = eventService;
        }

        [HttpPost]
        [Route("start")]
        public async Task<IActionResult> Post()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var cacheKey = CacheKey.RecoveryQueueKey + client.UserId.ToString();

            UserDataRecoveryCache cache = null;

            lock (_lock)
            {
                if (_memoryCache.TryGetValue(cacheKey, out cache))
                {
                    cache.Items = new List<UserDataRecoveryDto>();
                    cache.Logs = new List<string>();
                    cache.NotificationSent = false;
                }
                else
                {
                    cache = new UserDataRecoveryCache();
                    _memoryCache.Set(cacheKey, cache);
                }

                cache.Logs.Add($"User {client.UserId} starts recovery");

                foreach (var userPairing in _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingStatus == (int)UserPairingStatus.Accepted).ToList())
                {
                    string messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == userPairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();

                    cache.Logs.Add($"Sending push notifications to PI {userPairing.Id} PUI {userPairing.PairingUserId} MT {messagingToken}");

                    //var messageId = _firebaseService.SendBackgroundPushNotification(Guid.NewGuid().ToString(), messagingToken,
                    //    new
                    //    {
                    //        backupRecovery = new
                    //        {
                    //            mode = new
                    //            {
                    //                upload = new
                    //                {
                    //                    pairingId = userPairing.PairingToken
                    //                }
                    //            }
                    //        }
                    //    });

                    //cache.Logs.Add($"Push notification sent PI {userPairing.Id} PUI {userPairing.PairingUserId} MT {messagingToken} MID {messageId}");
                }
            }

            return Ok();
        }


        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Post([FromBody] UserDataRecoveryDto dataRecoveryDto)
        {
            if (dataRecoveryDto == null || String.IsNullOrWhiteSpace(dataRecoveryDto.PairingId) || String.IsNullOrWhiteSpace(dataRecoveryDto.Payload))
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var pairing = _l8p8DbContext.UserPairings.Where(p => p.PairingUserId == client.UserId && p.PairingToken == dataRecoveryDto.PairingId).SingleOrDefault();
            if (pairing != null)
            {
                var recoveringClient = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.UserId && c.DataVault.HasValue && c.DataVault.Value).FirstOrDefault();
                if (recoveringClient == null)
                    return Unauthorized();

                var cacheKey = CacheKey.RecoveryQueueKey + pairing.UserId.ToString();

                lock (_lock)
                {
                    UserDataRecoveryCache cache = null;

                    if (!_memoryCache.TryGetValue(cacheKey, out cache))
                    { 
                        cache = new UserDataRecoveryCache();
                        _memoryCache.Set(cacheKey, cache);
                    }

                    if (dataRecoveryDto.DataVersion != recoveringClient.VaultVersion)
                    {
                        cache.Logs.Add($"Data version doesn't match PID {dataRecoveryDto.PairingId} DV {dataRecoveryDto.DataVersion} RDV {recoveringClient.VaultVersion} PT {dataRecoveryDto.PayloadType}");
                    }
                    else
                    {
                        if (!cache.Items.Any(i => i.PairingId == dataRecoveryDto.PairingId))
                        {
                            cache.Items.Add(dataRecoveryDto);

                            var keyItems = cache.Items.Where(i => i.PayloadType?.ToLower() == "key").Count();
                            var dataItems = cache.Items.Where(i => i.PayloadType?.ToLower() == "data").Count();

                            cache.Logs.Add($"cache entry updated. {dataRecoveryDto.PairingId} {dataRecoveryDto.DataVersion} {dataRecoveryDto.PayloadType} - key count: {keyItems} data count: {dataItems}");

                            if (keyItems >= 4 && dataItems >= 2)
                            {
                                if (!cache.NotificationSent)
                                {
                                    string messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();

                                    cache.Logs.Add($"sending push notification");

                                    //_firebaseService.SendBackgroundPushNotification(Guid.NewGuid().ToString(), messagingToken, new { backupRecovery = new { mode = new { download = new { } } } });

                                    cache.NotificationSent = true;
                                }
                                else
                                {
                                    cache.Logs.Add($"skip sening push notifcation. already sent");
                                }
                            }
                        }
                        else
                        {
                            cache.Logs.Add($"Duplicate data received PID {dataRecoveryDto.PairingId} DV {dataRecoveryDto.DataVersion} PT {dataRecoveryDto.PayloadType}");
                        }
                    }
                }
            }
            else
            {
                return BadRequest();
            }

            return Ok();
        }


        [HttpGet]
        [Route("")]
        public async Task<IActionResult> Get()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var cacheKey = CacheKey.RecoveryQueueKey + client.UserId.ToString();
            var data = new UserDataRecovery();

            lock (_lock)
            {
                if (_memoryCache.TryGetValue(cacheKey, out UserDataRecoveryCache cache))
                {
                    if (cache != null && cache.Items != null && cache.Items.Count > 0)
                    {
                        data.DataVersion = cache.Items.FirstOrDefault().DataVersion;
                        data.Items = cache.Items.ToArray();
                        data.Logs = cache.Logs.ToArray();

                    }
                }
            }

            return Ok(data);
        }

        [HttpPost]
        [Route("status")]
        public async Task<IActionResult> PostConfirmation([FromBody] UserDataRecoveryConfirmationDto dataRecoveryConfirmationDto)
        {
            if (dataRecoveryConfirmationDto == null)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            // clear cache
            var cacheKey = CacheKey.RecoveryQueueKey + client.UserId.ToString();
            _memoryCache.Remove(cacheKey);

            return Ok();
        }

        [HttpGet]
        [Route("progress")]
        public async Task<IActionResult> GetProgress()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var data = new UserDataRecoveryProgressDto();

            // clear cache
            var cacheKey = CacheKey.RecoveryQueueKey + client.UserId.ToString();

            lock (_lock)
            {
                if (_memoryCache.TryGetValue(cacheKey, out UserDataRecoveryCache cache))
                {
                    if (cache != null && cache.Items != null && cache.Items.Count > 0)
                    {
                        data.DataVersion = cache.Items.FirstOrDefault().DataVersion;
                        data.ApprovedPairingIds = cache.Items.Select(i => i.PairingId).ToList();
                    }
                }
            }

            return Ok(data);
        }
    }

    public class UserDataRecoveryCache
    {
        public UserDataRecoveryCache()
        {
            Items = new List<UserDataRecoveryDto>();
            Logs = new List<string>();
            NotificationSent = false;
        }

        public List<UserDataRecoveryDto> Items { get; set; }

        public List<string> Logs { get; set; }

        public bool NotificationSent { get; set; }
    }

}
