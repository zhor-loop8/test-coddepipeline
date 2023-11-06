using Amazon.Runtime.Internal.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly ILogger<HomeController> _logger;
        private readonly l8p8Context _dbContext;
        private readonly IDistributedCache _cache;
        private readonly SettingService _settingService;
        private readonly IConfiguration _configuration;
        private readonly StorageService _storageService;
        private readonly SecretService _secretService;

        public HomeController(ILogger<HomeController> logger, l8p8Context dbContext, IDistributedCache cache, SettingService settingService, IConfiguration configuration, StorageService storageService, SecretService secretService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _cache = cache;
            _settingService = settingService;
            _configuration = configuration;
            _storageService = storageService;
            _secretService = secretService;
        }

        [HttpGet]
        [Route("/")]
        public async Task<IActionResult> Get()
        {
            return Ok("200 - " + _configuration.GetValue<string>("Environment"));
        }

        [HttpGet]
        [Route("/health-check")]
        public async Task<IActionResult> GetHealthCheck()
        {
            if (!String.IsNullOrWhiteSpace(_secretService.GetSecretValue("FCMSettings")))
            {
                return Ok("200 - " + _configuration.GetValue<string>("Environment"));
            }

            return Ok();
        }


        [HttpGet]
        [Route("/api/server-time")]
        public  object GetServerTime()
        {
            return new { server_time = DateTime.UtcNow.ToString("u").Replace(" ", "T")  };
        }


        //[HttpGet]
        //[Route("/cache")]
        //public async void TestCache()
        //{
        //    var msg = new CachedMessage();
        //    msg.MessageId = Guid.NewGuid().ToString();
        //    msg.Request = "json";
        //    msg.RequestTimestamp = DateTime.Now;
        //    msg.CustomFields.Add("Push Notification");
        //    msg.CustomFields.Add((string)Request.Headers["X-Device-Id"]);
        //    msg.CustomFields.Add("erCw69Q9RB-h2sgch2pj4b:APA91bGJL74ODISkKrbbbH6CwXzcaA2vaQsdJJ-yVlW7E-fQ8RNGKagrQt4HCi7ICXqcPiZxOr9-LZuh7uEb8F_QhG0u2-zwFLrHuJqOE15ra2GUYRglETCw84Ia0z-oeQy2YLWf3lMZ");

        //    var evt = new EventDto();
        //    evt.Id = Guid.NewGuid().ToString();
        //    evt.Type = JsonSerializer.Serialize(new { clientMessage = new { messageId = msg.MessageId, messageData = "json" } });

        //    //try
        //    //{
        //    //set the cached message
        //    await _cache.SetStringAsync(msg.MessageId, JsonSerializer.Serialize(msg), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        //}


        //[HttpPost]
        //[Route("/file")]
        //public async Task<bool> MultipartUploadFile(IFormFile file)
        //{
        //    await _storageService.MultipartUploadFile(file);
        //    return true;
        //}


        //[HttpGet]
        //[Route("/data-stats")]
        //public async Task<IActionResult> GetDataStats()
        //{
        //    try
        //    {
        //        var users = _dbContext.Users.Count();
        //        var userPairings = await _dbContext.UserPairings.CountAsync();
        //        var clients = await _dbContext.Clients.CountAsync();
        //        var clientPairings = await _dbContext.ClientPairings.CountAsync();

        //        return Ok($"users: {users}, user pairings: {userPairings}, clients {clients}, clientPairings {clientPairings}");
        //    }
        //    catch (Exception ex)
        //    {
        //        return Ok(ex.ToString());
        //    }
        //}

        //[HttpGet]
        //[Route("/cache-stats")]
        //public async Task<IActionResult> GetCacheStats()
        //{
        //    await _cache.SetStringAsync("CacheTime", DateTime.UtcNow.ToString(), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

        //    string val = await _cache.GetStringAsync("CacheTime");

        //    return Ok(val);
        //}
    }
}
