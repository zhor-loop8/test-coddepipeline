using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    [Route("api/message")]
    public class MessageController : ControllerBase
    {
        private readonly ILogger<MessageController> _logger;
        private readonly IDistributedCache _cache;
        private readonly l8p8Context _l8p8DbContext;
        private readonly FirebaseService _firebaseService;
        private readonly EventService _eventService;

        public MessageController(ILogger<MessageController> logger, IDistributedCache cache, l8p8Context l8p8DbContext, FirebaseService firebaseService, EventService eventService)
        {
            _logger = logger;
            _cache = cache;
            _l8p8DbContext = l8p8DbContext;
            _firebaseService = firebaseService;
            _eventService = eventService;
        }

        [HttpPost]
        [Route("background")]
        public async Task<IActionResult> PostBackgroundMessagee([FromBody] object json)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
            if (String.IsNullOrWhiteSpace(messagingToken))
                return Unauthorized();

            var msg = new CachedMessage();
            msg.MessageId = Guid.NewGuid().ToString();
            msg.Request = json;
            msg.RequestTimestamp = DateTime.Now;
            msg.CustomFields.Add("Silent Push Notification");
            msg.CustomFields.Add((string)Request.Headers["X-Device-Id"]);
            msg.CustomFields.Add(messagingToken);

            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { clientMessage = new { messageId = msg.MessageId, messageData = json } });

            try
            {
                //set the cached message
                await _cache.SetStringAsync(msg.MessageId, JsonSerializer.Serialize(msg), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

                //set the event for the phone
                await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

                Event ev = new Event()
                {
                    UserId = (long)client.UserId,
                    TypeId = EventTypeEnum.ClientMessage,
                    EventId = new Guid(evt.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev);
                _l8p8DbContext.SaveChanges();

                //var clientEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString());
                //if (!String.IsNullOrWhiteSpace(clientEventIdsJson))
                //{
                //    //there are pending requests.  add the new request to the list
                //    //we could also reject here to enforece single request at a time
                //    var clientEventIds = JsonSerializer.Deserialize<List<string>>(clientEventIdsJson);
                //    clientEventIds.Add(evt.Id);
                //    await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
                //}
                //else
                //{
                //    List<string> clientEventIds = new List<string>();
                //    clientEventIds.Add(evt.Id);
                //    await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
                //}

                var result = _firebaseService.SendBackgroundPushNotification(msg.MessageId, messagingToken);

                msg.CustomFields.Add("Silent Sent at: " + DateTime.UtcNow.ToString());

                //wait 5 seconds for the phone to respond to the silent push by checking if the cachedMessage contains a response object
                var cachedMsg = await Observable.Interval(TimeSpan.FromMilliseconds(250))
                    .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(5)))
                    .Select(_ => { var value = _cache.GetString(msg.MessageId); return String.IsNullOrWhiteSpace(value) ? (CachedMessage)null : JsonSerializer.Deserialize<CachedMessage>(value); })
                    .Where(m => m != null && m.Response != null)
                    .FirstOrDefaultAsync();

                if (cachedMsg == null)
                {
                    msg.CustomFields.Add(result);
                    msg.CustomFields.Add("Timed out at: " + DateTime.UtcNow.ToString());

                    result = _firebaseService.SendPushNotification(msg.MessageId, messagingToken, client.ClientName ?? "Data Access Request", "Data Access Requested");

                    msg.CustomFields.Add("Sent at: " + DateTime.UtcNow.ToString());

                    //wait 30 seconda for the phone to respond by checking if the cachedMessage contains a response object
                    cachedMsg = await Observable.Interval(TimeSpan.FromMilliseconds(500))
                        .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(30)))
                        .Select(_ => { var value = _cache.GetString(msg.MessageId); return String.IsNullOrWhiteSpace(value) ? (CachedMessage)null : JsonSerializer.Deserialize<CachedMessage>(value); })
                        .Where(m => m != null && m.Response != null)
                        .FirstOrDefaultAsync();
                }


                if (cachedMsg != null)
                {
                    //TBD: avoid updating database 
                    if (!String.IsNullOrWhiteSpace(cachedMsg.DataVersion))
                    {
                        Response.Headers["X-Data-Version-Id"] = cachedMsg.DataVersion;

                        _l8p8DbContext.Clients.Where(c => c.MessagingToken == messagingToken).ToList().ForEach(c => c.VaultVersion = cachedMsg.DataVersion);
                        _l8p8DbContext.SaveChanges();
                    }

                    return Ok(cachedMsg.Response);
                }
                else
                {
                    msg.CustomFields.Add(result);
                    msg.CustomFields.Add("Timed out at: " + DateTime.UtcNow.ToString());

                    _logger.LogDebug(JsonSerializer.Serialize(msg));

                    return StatusCode(408);
                }

            }
            catch (FirebaseMessagingException fmex)
            {
                msg.CustomFields.Add(fmex.ErrorCode.ToString());
                _logger.LogError($"MessageController:Post push failed - {fmex.ErrorCode}");
            }
            catch (Exception ex)
            {
                msg.CustomFields.Add(ex.Message);
                _logger.LogError(ex, "MessageController:Post failed");
            }

            _logger.LogDebug(JsonSerializer.Serialize(msg));

            return StatusCode(424);
        }

        [HttpPost]
        public async Task<IActionResult> PostMessage([FromBody] object json)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
            if (String.IsNullOrWhiteSpace(messagingToken))
                return Unauthorized();

            var msg = new CachedMessage();
            msg.MessageId = Guid.NewGuid().ToString();
            msg.Request = json;
            msg.RequestTimestamp = DateTime.Now;
            msg.CustomFields.Add("Push Notification");
            msg.CustomFields.Add((string)Request.Headers["X-Device-Id"]);
            msg.CustomFields.Add(messagingToken);

            var evt = new EventDto();
            evt.Id = Guid.NewGuid().ToString();
            evt.Type = JsonSerializer.Serialize(new { clientMessage = new { messageId = msg.MessageId, messageData = json } });

            //try
            //{
                //set the cached message
                await _cache.SetStringAsync(msg.MessageId, JsonSerializer.Serialize(msg), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

                //set the event for the phone
                await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

                Event ev = new Event()
                {
                    UserId = (long)client.UserId,
                    TypeId = EventTypeEnum.ClientMessage,
                    EventId = new Guid(evt.Id),
                    ExpirationDate = DateTime.UtcNow.AddDays(14),
                    Acknowledged = false,
                    InsertDate = DateTime.UtcNow,
                };
                _l8p8DbContext.Add(ev);
                _l8p8DbContext.SaveChanges();

                //var clientEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString());
                //if (!String.IsNullOrWhiteSpace(clientEventIdsJson))
                //{
                //    //there are pending requests.  add the new request to the list
                //    //we could also reject here to enforece single request at a time
                //    var clientEventIds = JsonSerializer.Deserialize<List<string>>(clientEventIdsJson);
                //    clientEventIds.Add(evt.Id);
                //    await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
                //}
                //else
                //{
                //    List<string> clientEventIds = new List<string>();
                //    clientEventIds.Add(evt.Id);
                //    await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
                //}

                var result = _firebaseService.SendPushNotification(msg.MessageId, messagingToken, client.ClientName ?? "Data Access Request", "Data Access Requested");

                msg.CustomFields.Add("Sent at: " + DateTime.UtcNow.ToString());

                //wait for the phone to respond by checking if the cachedMessage contains a response object
                var cachedMsg = await Observable.Interval(TimeSpan.FromMilliseconds(500))
                    .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(30)))
                    .Select(_ => { var value = _cache.GetString(msg.MessageId); return String.IsNullOrWhiteSpace(value) ? (CachedMessage)null : JsonSerializer.Deserialize<CachedMessage>(value); })
                    .Where(m => m != null && m.Response != null)
                    .FirstOrDefaultAsync();

                if (cachedMsg != null)
                {
                    //TBD: avoid updating database 
                    if (!String.IsNullOrWhiteSpace(cachedMsg.DataVersion))
                    {
                        Response.Headers["X-Data-Version-Id"] = cachedMsg.DataVersion;

                        _l8p8DbContext.Clients.Where(c => c.MessagingToken == messagingToken).ToList().ForEach(c => c.VaultVersion = cachedMsg.DataVersion);
                        _l8p8DbContext.SaveChanges();
                    }

                    return Ok(cachedMsg.Response);
                }
                else
                {
                    msg.CustomFields.Add(result);
                    msg.CustomFields.Add("Timed out at: " + DateTime.UtcNow.ToString());

                    _logger.LogDebug(JsonSerializer.Serialize(msg));

                    return StatusCode(408);
                }

            //}
            //catch (FirebaseMessagingException fmex)
            //{
            //    msg.CustomFields.Add(fmex.ErrorCode.ToString());
            //    _logger.LogError($"MessageController:Post push failed - {fmex.ErrorCode}");
            //}
            //catch (Exception ex)
            //{
            //    msg.CustomFields.Add(ex.Message);
            //    _logger.LogError(ex, "MessageController:Post failed");
            //}

            _logger.LogDebug(JsonSerializer.Serialize(msg));

            return StatusCode(424);
        }



        //[HttpPost]
        //public async Task<IActionResult> Post([FromBody] object json)
        //{
        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
        //    if (client == null)
        //        return Unauthorized();

        //    var messagingToken = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.DataVault.HasValue && c.DataVault.Value).Select(c => c.MessagingToken).FirstOrDefault();
        //    if (String.IsNullOrWhiteSpace(messagingToken))
        //        return Unauthorized();

        //    var msg = new CachedMessage();
        //    msg.MessageId = Guid.NewGuid().ToString();
        //    msg.Request = json;
        //    msg.RequestTimestamp = DateTime.Now;
        //    msg.CustomFields.Add("Push Notification");
        //    msg.CustomFields.Add((string)Request.Headers["X-Device-Id"]);
        //    msg.CustomFields.Add(messagingToken);

        //    var evt = new EventDto();
        //    evt.Id = Guid.NewGuid().ToString();
        //    evt.Type = JsonSerializer.Serialize(new { clientMessage = new { messageId = msg.MessageId, messageData = json } });

        //    try
        //    {
        //        //set the cached message
        //        await _cache.SetStringAsync(msg.MessageId, JsonSerializer.Serialize(msg), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

        //        //set the event for the phone
        //        await _cache.SetStringAsync(evt.Id, JsonSerializer.Serialize(evt), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        //        var clientEventIdsJson = await _cache.GetStringAsync(client.UserId.ToString());
        //        if (!String.IsNullOrWhiteSpace(clientEventIdsJson))
        //        {
        //            //there are pending requests.  add the new request to the list
        //            //we could also reject here to enforece single request at a time
        //            var clientEventIds = JsonSerializer.Deserialize<List<string>>(clientEventIdsJson);
        //            clientEventIds.Add(evt.Id);
        //            await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        //        }
        //        else
        //        {
        //            List<string> clientEventIds = new List<string>();
        //            clientEventIds.Add(evt.Id);
        //            await _cache.SetStringAsync(client.UserId.ToString(), JsonSerializer.Serialize(clientEventIds), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        //        }

        //        var result = _firebaseService.SendPushNotification(msg.MessageId, messagingToken, client.ClientName ?? "Data Access Request", "Data Access Requested");

        //        msg.CustomFields.Add("Sent at: " + DateTime.UtcNow.ToString());

        //        //wait for the phone to respond by checking if the cachedMessage contains a response object
        //        var cachedMsg = await Observable.Interval(TimeSpan.FromMilliseconds(500))
        //            .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(30)))
        //            .Select(_ => { var value = _cache.GetString(msg.MessageId); return String.IsNullOrWhiteSpace(value) ? (CachedMessage)null : JsonSerializer.Deserialize<CachedMessage>(value); })
        //            .Where(m => m != null && m.Response != null)
        //            .FirstOrDefaultAsync();

        //        if (cachedMsg != null)
        //        {
        //            //TBD: avoid updating database 
        //            if (!String.IsNullOrWhiteSpace(cachedMsg.DataVersion))
        //            {
        //                Response.Headers["X-Data-Version-Id"] = cachedMsg.DataVersion;

        //                _l8p8DbContext.Clients.Where(c => c.MessagingToken == messagingToken).ToList().ForEach(c => c.VaultVersion = cachedMsg.DataVersion);
        //                _l8p8DbContext.SaveChanges();
        //            }

        //            return Ok(cachedMsg.Response);
        //        }
        //        else
        //        {
        //            msg.CustomFields.Add(result);
        //            msg.CustomFields.Add("Timed out at: " + DateTime.UtcNow.ToString());

        //            _logger.LogDebug(JsonSerializer.Serialize(msg));

        //            return StatusCode(408);
        //        }

        //    }
        //    catch (FirebaseMessagingException fmex)
        //    {
        //        msg.CustomFields.Add(fmex.ErrorCode.ToString());
        //        _logger.LogError($"MessageController:Post push failed - {fmex.ErrorCode}");
        //    }
        //    catch (Exception ex)
        //    {
        //        msg.CustomFields.Add(ex.Message);
        //        _logger.LogError(ex, "MessageController:Post failed");
        //    }

        //    _logger.LogDebug(JsonSerializer.Serialize(msg));

        //    return StatusCode(424);
        //}

        [HttpPost]
        [Route("{messageId}/response")]
        public async Task<IActionResult> PostMessageResponse([FromRoute] string messageId, [FromBody] object json)
        {
            //if (!Request.Headers.ContainsKey("Authorization"))
            //    return Unauthorized();

            //if (Request.Headers["Authorization"] != "712eb6c7-e5ec-4a56-b5a3-df9a0959030d")
            //    return Unauthorized();

            //if (!Request.Headers.ContainsKey("Authorization"))
            //    return Unauthorized();

            var value = await _cache.GetStringAsync(messageId);
            if (!String.IsNullOrWhiteSpace(value))
            {
                var msg = JsonSerializer.Deserialize<CachedMessage>(value);
                msg.Response = json;
                msg.ResponseTimestamp = DateTime.Now;
                msg.DataVersion = (string)Request.Headers["X-Data-Version-Id"];

                await _cache.SetStringAsync(messageId, JsonSerializer.Serialize(msg), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

                return Ok();
            }
            else
            {
                return NotFound();
            }


            //if (_memoryCache.TryGetValue(messageId, out MessageCache messageCache))
            //{
            //    messageCache.Response = json;
            //    messageCache.ResponseTimestamp = DateTime.Now;
            //    messageCache.DataVersion = (string)Request.Headers["X-Data-Version-Id"];

            //    return Ok();
            //}
            //else
            //{
            //    return NotFound();
            //}
        }

        //[HttpGet]
        //[Route("log")]
        //public async Task<IActionResult> Get()
        //{
        //    if (!Request.Headers.ContainsKey("Authorization"))
        //        return Unauthorized();

        //    if (Request.Headers["Authorization"] != "712eb6c7-e5ec-4a56-b5a3-df9a0959030d")
        //        return Unauthorized();

        //    if (_memoryCache.TryGetValue(CacheKey.LastMessageIdKey, out string messageId))
        //    {
        //        if (_memoryCache.TryGetValue(messageId, out MessageCache messageCache))
        //        {
        //            return Ok(messageCache);
        //        }
        //    }

        //    return NotFound();
        //}
    }
}
