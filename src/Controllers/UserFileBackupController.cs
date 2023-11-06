using Amazon.SecretsManager.Model;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models.UserFileBackup;
using WebAPI.Models.UserPairing;
using WebAPI.Services;
using static Google.Apis.Requests.BatchRequest;

namespace WebAPI.Controllers
{
    [ApiController]
    //[ApiVersion("1.0")]
    [Route("api/backup/file")]
    public class UserFileBackupController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MessageController> _logger;
        private readonly l8p8Context _l8p8DbContext;
        private readonly StorageService _storageService;

        public UserFileBackupController(IConfiguration configuration, ILogger<MessageController> logger, l8p8Context l8p8DbContext, StorageService storageService)
        {
            _configuration = configuration;
            _logger = logger;
            _l8p8DbContext = l8p8DbContext;
            _storageService = storageService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] UserFile file)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            if (file == null || String.IsNullOrEmpty(file.Filename) || file.Content == null)
                return BadRequest();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(client.BackupFolderId))
                return Forbid();

            var usage = GetBackupUsage(client.UserId).Result;
            if (usage.Capacity < usage.Size + file.Content.Length)
            {
                return StatusCode(507, "Not enough storage");
            }
            else
            {
                string bucket = _configuration.GetValue<string>("StorageBucket");
                string location = $"{client.UserId}/{client.BackupFolderId}/{file.Filename}";

                _storageService.UploadFile(bucket, location, file.ContentType, file.Content);

                return Ok();
            }
        }

        [HttpGet]
        [Produces(typeof(UserFile))]
        public async Task<IActionResult> Get(string filename)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(client.BackupFolderId))
                return Forbid();

            string bucket = _configuration.GetValue<string>("StorageBucket");
            string location = $"{client.UserId}/{client.BackupFolderId}/{filename}";

            var data = _storageService.GetFile(bucket, location).Result;
            if (data == null)
            {
                return NotFound();
            }

            return Ok(new UserFile { Filename = filename, Content = data });
        }

        [Route("/api/backup/file-listing")]
        [HttpGet]
        [Produces(typeof(UserFileListing))]
        public async Task<IActionResult> GetListing()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(client.BackupFolderId))
                return Forbid();

            string bucket = _configuration.GetValue<string>("StorageBucket");
            string location = $"{client.UserId}/{client.BackupFolderId}/";

            var data = _storageService.ListFiles(bucket, location).Result;
            var list = data.Select(o => o.Item1.Replace(location, "")).ToList();

            return Ok(new UserFileListing { Files = list });
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(string filename)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(client.BackupFolderId))
                return Forbid();

            string bucket = _configuration.GetValue<string>("StorageBucket");
            string location = $"{client.UserId}/{client.BackupFolderId}/{filename}";

            _storageService.DeleteFile(bucket, location);

            return Ok();
        }


        [HttpGet]
        [Route("/api/backup/folder")]
        [Produces(typeof(UserFolder))]
        public async Task<IActionResult> GetFolder()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(client.BackupFolderId))
                return NotFound();

            return Ok(new UserFolder() { FolderId = client.BackupFolderId, FolderName = client.BackupFolderName, FolderCreationDate = client.BackupFolderCreationDate.HasValue ? client.BackupFolderCreationDate.Value : DateTime.MinValue });
        }

        [HttpPut]
        [Route("/api/backup/folder")]
        [Produces(typeof(UserFolder))]
        public async Task<IActionResult> PutFolder([FromBody] UserFolder folder)
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(folder.FolderId) && String.IsNullOrWhiteSpace(folder.FolderName))
                return BadRequest();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(folder.FolderId))
            {
                client.BackupFolderId = Guid.NewGuid().ToString();
                client.BackupFolderName = folder.FolderName;
                client.BackupFolderCreationDate = DateTime.UtcNow;
            }
            else
            {
                var otherClient = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.Id != client.Id && c.BackupFolderId == folder.FolderId).FirstOrDefault();
                if (otherClient == null)
                    return Unauthorized();

                otherClient.BackupFolderId = null;
                otherClient.BackupFolderName = null;
                otherClient.BackupFolderCreationDate = null;

                client.BackupFolderId = folder.FolderId;
                client.BackupFolderName = folder.FolderName;
                client.BackupFolderCreationDate = folder.FolderCreationDate;
            }

            _l8p8DbContext.SaveChanges();

            return Ok();
        }

        [HttpDelete]
        [Route("/api/backup/folder")]
        [Produces(typeof(UserFolder))]
        public async Task<IActionResult> DeleteFolder()
        {
            //TODO: consider offline deletion

            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            if (String.IsNullOrWhiteSpace(client.BackupFolderId))
                return NotFound();

            string bucket = _configuration.GetValue<string>("StorageBucket");
            string location = $"{client.UserId}/{client.BackupFolderId}/";

            _logger.LogInformation($"S3: Deleting backup folder {location}");

            _storageService.DeleteFolder(bucket, location);

            client.BackupFolderId = null;
            client.BackupFolderName = null;
            client.BackupFolderCreationDate = null;

            _l8p8DbContext.SaveChanges();

            return Ok(new UserFolder() { FolderId = client.BackupFolderId, FolderName = client.BackupFolderName, FolderCreationDate = client.BackupFolderCreationDate.HasValue ? client.BackupFolderCreationDate.Value : DateTime.MinValue });
        }

        [HttpGet]
        [Route("/api/backup/recovery-folders")]
        [Produces(typeof(List<UserFolder>))]
        public async Task<IActionResult> GetRecoveryFolders()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var folders = _l8p8DbContext.Clients.Where(c => c.UserId == client.UserId && c.Id != client.Id && !String.IsNullOrWhiteSpace(c.BackupFolderId))
                .Select(c => new UserFolder() { FolderId = c.BackupFolderId, FolderName = c.BackupFolderName, FolderCreationDate = client.BackupFolderCreationDate.HasValue ? client.BackupFolderCreationDate.Value : DateTime.MinValue }).ToList();

            return Ok(folders);
        }

        [HttpGet]
        [Route("/api/backup/size")]
        [Route("/api/backup/sise")] // REMOVE IN NEXT RELEASE
        [Produces(typeof(UserFolderUsage))]
        public async Task<IActionResult> GetBackupSize()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();

            var client = _l8p8DbContext.Clients.Where(c => c.AuthenticationToken == (string)Request.Headers["Authorization"]).FirstOrDefault();
            if (client == null)
                return Unauthorized();

            var usage = GetBackupUsage(client.UserId).Result;  //TODO: User ID should be NOT NULL

            return Ok(usage);
        }

        private async Task<UserFolderUsage> GetBackupUsage(long userId)
        {
            //TODO: find a better way to calculate size

            var usage = new UserFolderUsage();
            usage.Capacity = 200 * 1048576;  // TODO: refactor
            usage.Folders = new List<UserFolder>();

            string bucket = _configuration.GetValue<string>("StorageBucket");
            string location = $"{userId}/";

            var data = _storageService.ListFiles(bucket, location).Result;

            string[] ghostFiles = new string[] { "manifest.archive" };

            foreach (var file in data)
            {
                if (ghostFiles.Contains(file.Item1))
                {
                    continue;
                }
                var folderId = file.Item1.Substring(location.Length, file.Item1.IndexOf("/", location.Length) - location.Length);

                var folder = usage.Folders.FirstOrDefault(f => f.FolderId == folderId);
                if (folder == null)
                {
                    folder = new UserFolder();
                    folder.FolderId = folderId;
                    folder.FolderSize = 0;
                    usage.Folders.Add(folder);
                }
                folder.FolderSize += file.Item2;
            }

            usage.Size = usage.Folders.Sum(f => f.FolderSize.HasValue ? f.FolderSize.Value : 0);

            var clients = _l8p8DbContext.Clients.Where(c => c.UserId == userId).ToList();
            foreach (var c in clients)
            {
                if (!String.IsNullOrWhiteSpace(c.BackupFolderId))
                {
                    var folder = usage.Folders.FirstOrDefault(f => f.FolderId == c.BackupFolderId);
                    if (folder != null)
                    {
                        folder.FolderName = c.BackupFolderName;
                        folder.FolderCreationDate = c.BackupFolderCreationDate.HasValue ? c.BackupFolderCreationDate.Value : DateTime.MinValue;
                    }
                }
            }

            return usage;
        }

    }
}
