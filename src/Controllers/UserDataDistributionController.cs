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
    [Route("api/backup/distribution")]
    public class UserDataDistributionController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MessageController> _logger;
        private readonly l8p8Context _l8p8DbContext;
        private readonly PhoneService _phoneService;
        private readonly MessageService _messageService;
        private readonly FirebaseService _firebaseService;
        private readonly EventService _eventService;
        private readonly object _lock = new object();

        public UserDataDistributionController(IMemoryCache memoryCache, ILogger<MessageController> logger, l8p8Context l8p8DbContext, PhoneService phoneService, MessageService messageService, FirebaseService firebaseService, EventService eventService)
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
        [Route("test-upload")]
        public async Task<IActionResult> Post()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            string messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();

            //_firebaseService.SendBackgroundPushNotification(Guid.NewGuid().ToString(), messagingToken, new { backupDistribution = new { mode = new { upload = new { } } } });

            return Ok();
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> Post([FromBody] UserDataDistributionDto dataDistributionDto)
        {
            if (dataDistributionDto == null || dataDistributionDto.Items == null || dataDistributionDto.Items.Length == 0)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            client.VaultVersion = dataDistributionDto.DataVersion;
            _l8p8DbContext.SaveChanges();

            foreach (var item in dataDistributionDto.Items)
            {
                var pairing = _l8p8DbContext.UserPairings.Where(p => p.UserId == client.UserId && p.PairingToken == item.PairingId).SingleOrDefault();
                if (pairing != null)
                {
                    var cacheKey = CacheKey.DistributionQueueKey + pairing.PairingUserId.ToString();

                    var data = new UserDataDistributionPairing();
                    data.DataVersion = dataDistributionDto.DataVersion;
                    data.Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    data.PairingId = item.PairingId;
                    data.PayloadType = item.PayloadType;
                    data.Payload = item.Payload;

                    lock (_lock)
                    {

                        if (_memoryCache.TryGetValue(cacheKey, out UserDataDistributionCache cache))
                        {
                            cache.Items.Add(data);
                        }
                        else
                        {
                            cache = new UserDataDistributionCache();
                            cache.Items = new List<UserDataDistributionPairing>();
                            cache.Items.Add(data);

                            _memoryCache.Set(cacheKey, cache);
                        }
                    }

                    string messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == pairing.PairingUserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();

                    //_firebaseService.SendBackgroundPushNotification(Guid.NewGuid().ToString(), messagingToken, new { backupDistribution = new { mode = new { download = new { } } } });
                }
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

            var cacheKey = CacheKey.DistributionQueueKey + client.UserId.ToString();
            var data = new UserDataDistribution();

            lock (_lock)
            {
                if (_memoryCache.TryGetValue(cacheKey, out UserDataDistributionCache cache))
                {
                    data.Items = cache.Items.OrderBy(i => i.Timestamp).ToArray();
                }
            }

            return Ok(data);
        }

        [HttpPost]
        [Route("status")]
        public async Task<IActionResult> PostConfirmation([FromBody] UserDataDistributionConfirmationDto dataDistributionConfirmationDto)
        {
            if (dataDistributionConfirmationDto == null || dataDistributionConfirmationDto.Items == null || dataDistributionConfirmationDto.Items.Length == 0)
                return BadRequest();

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var cacheKey = CacheKey.DistributionQueueKey + client.UserId.ToString();
            var data = new UserDataDistributionDto();

            lock (_lock)
            {
                if (_memoryCache.TryGetValue(cacheKey, out UserDataDistributionCache cache))
                {
                    foreach (var item in dataDistributionConfirmationDto.Items)
                    {
                        if (item.Success)
                        {
                            var cacheItem = cache.Items.Find(i => i.DataVersion == item.DataVersion && i.PairingId == item.PairingId);
                            if (cacheItem != null)
                            {
                                cache.Items.Remove(cacheItem);
                            }
                        }
                        else
                        {
                            // TBD: log error?
                        }
                    }
                }
            }

            return Ok();
        }
    }

    public class UserDataDistributionCache
    {
        public List<UserDataDistributionPairing> Items { get; set; }
    }
}
