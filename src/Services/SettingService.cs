using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text.Json.Nodes;

namespace WebAPI.Services
{
    public class SettingService
    {
        private readonly IConfiguration _configuration;
        private readonly SecretService _secretService;


        public SettingService(IConfiguration configuration, SecretService secretService)
        {
            _configuration = configuration;
            _secretService = secretService;
        }

        public string GetDatabaseConnectionString()
        {
            var secretName = _configuration.GetValue<string>("DatabaseConnectionSecretName");
            var secretValue = _secretService.GetSecretValue(secretName);

            var json = JsonNode.Parse(secretValue);

            return $"Host={json["host"]};Port=5432;Database=l8p8;User Id={json["username"]};Password={json["password"]};";
        }
    }
}
