using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;
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
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/version")]
    public class VersionController : ControllerBase
    {
        private readonly ILogger<MessageController> _logger;
        private readonly l8p8Context _l8p8DbContext;


        public VersionController(IMemoryCache memoryCache, ILogger<MessageController> logger, l8p8Context l8p8DbContext)
        {
            _logger = logger;
            _l8p8DbContext = l8p8DbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetVersion()
        {
            var version = new VersionModel();

            if (Request.Headers.ContainsKey("Authorization"))
            {
                var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
                if (client == null)
                    return Unauthorized();

                var mainClient = _l8p8DbContext.Clients.Where(c => c.DataVault == true && c.UserId == client.UserId).FirstOrDefault();
                if (mainClient == null)
                    return Unauthorized();

                version.DataVersion = mainClient.VaultVersion;
            }
            else
            {
                return Unauthorized();
            }

            return Ok(version);
        }

        [HttpPut]
        [Route("/api/vault/version")]
        public async Task<IActionResult> Post([FromBody] VersionModel version)
        {
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
                if (client == null)
                    return Unauthorized();

                var mainClient = _l8p8DbContext.Clients.Where(c => c.DataVault == true && c.UserId == client.UserId).FirstOrDefault();
                if (mainClient == null)
                    return Unauthorized();

                mainClient.VaultVersion = version.DataVersion;

                _l8p8DbContext.SaveChanges();
            }
            else
            {
                return Unauthorized();
            }

            return Ok();
        }


        [HttpGet]
        [Route("/api/vault/version")]
        public async Task<IActionResult> Get()
        {
            var version = new VersionModel();

            if (Request.Headers.ContainsKey("Authorization"))
            {
                var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
                if (client == null)
                    return Unauthorized();

                var mainClient = _l8p8DbContext.Clients.Where(c => c.DataVault == true && c.UserId == client.UserId).FirstOrDefault();
                if (mainClient == null)
                    return Unauthorized();

                version.DataVersion = mainClient.VaultVersion;
            }
            else
            {
                return Unauthorized();
            }

            return Ok(version);
        }
    }
}
