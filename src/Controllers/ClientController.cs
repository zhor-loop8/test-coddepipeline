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
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<AccountController> _logger;
        private readonly l8p8Context _dbContext;

        public ClientController(IMemoryCache memoryCache, ILogger<AccountController> logger, l8p8Context dbContext)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpPost]
        [Route("/api/pairing")]
        public async Task<IActionResult> Post([FromBody] ClientPairingDto clientPairingDto)
        {
            if (String.IsNullOrWhiteSpace(clientPairingDto.PairingToken) ||
                String.IsNullOrWhiteSpace(clientPairingDto.PairingClientId) ||
//              String.IsNullOrWhiteSpace(clientPairingDto.PairingClientVersion) ||
                String.IsNullOrWhiteSpace(clientPairingDto.PairingPayload))
            {
                return BadRequest();
            }

            //TODO: cache
            //var clientTypeId = _dbContext.ClientTypes.ToList().Where(t => $"{t.Type}-{t.Secret}" == clientPairingDto.PairingClientVersion).Select(t => t.Id).FirstOrDefault();
            //if (clientTypeId == 0)
            //{
            //    return BadRequest();
            //}

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _dbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
            {
                return Unauthorized();
            }

            var pairingClient = new Client();
            pairingClient.UserId = client.UserId;
            pairingClient.Gdi = "TBD";
            pairingClient.ClientId = clientPairingDto.PairingClientId;
            pairingClient.ClientName = clientPairingDto.PairingClientName;
            //          pairingClient.ClientTypeId = clientTypeId;
            pairingClient.MessagingToken = null;
            pairingClient.AuthenticationToken = Guid.NewGuid().ToString();
            pairingClient.DataVault = false;
            pairingClient.ConfidenceScore = 100;
            pairingClient.InsertDate = DateTime.UtcNow;
            pairingClient.UpdateDate = DateTime.UtcNow;

            _dbContext.Clients.Add(pairingClient);
            _dbContext.SaveChanges();

            var pairing = new ClientPairing();
            pairing.ClientId = client.Id;
            pairing.PairingClientId = pairingClient.Id;
            pairing.PairingToken = clientPairingDto.PairingToken;
            pairing.PairingTokenExpiration = DateTime.UtcNow.AddMinutes(5);
            pairing.PairingPayload = clientPairingDto.PairingPayload;
            pairing.Paired = false;
            pairing.InsertDate = DateTime.UtcNow;
            pairing.UpdateDate = DateTime.UtcNow;

            _dbContext.ClientPairings.Add(pairing);
            _dbContext.SaveChanges();

            return Ok();
        }

        [HttpGet]
        [Route("/api/pairing")]
        public async Task<IActionResult> Get([FromRoute] string token)
        {
            if (!Request.Headers.ContainsKey("X-Pairing-Token"))
                return BadRequest();

            DateTime expiration = DateTime.Now.AddSeconds(30);

            ClientPairing pairing = null;

            do
            {
                pairing = _dbContext.ClientPairings
                    .Where(p => p.PairingToken == (string)Request.Headers["X-Pairing-Token"])
                    .Include(p => p.PairingClient)
                    .FirstOrDefault();

                if (pairing == null)
                {
                    Thread.Sleep(2000);
                }
                else
                {
                    break;
                }
            }
            while (DateTime.Now < expiration);

            if (pairing == null)
            {
                return StatusCode(204);
            }

            if (pairing.Paired.HasValue && pairing.Paired.Value || pairing.PairingTokenExpiration < DateTime.UtcNow)
            {
                return BadRequest();
            }

            pairing.Paired = true;
            pairing.UpdateDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(new { pairing.PairingClient.AuthenticationToken, pairing.PairingPayload });
        }

        [HttpGet]
        [Route("/api/me")]
        public async Task<IActionResult> Get()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _dbContext.Clients
                .Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"])
                .Include(c => c.User)
                .FirstOrDefault();

            if (client == null)
            {
                return Unauthorized();
            }

            return Ok(new { Account = new { DisplayName = client.User.Email, CreationDateUtc = client.User.InsertDate } });
        }
    }
}
