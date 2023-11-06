using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.FirebaseDynamicLinks.v1;
using Google.Apis.FirebaseDynamicLinks.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using WebAPI.Controllers;

namespace WebAPI.Services
{
    public class FirebaseService
    {
        private readonly IConfiguration _configuration;
        private readonly SecretService _secretService;
        private readonly ILogger<FirebaseService> _logger;

        static FirebaseService()
        {
            //if (FirebaseApp.DefaultInstance == null)
            //{
            //    string firebaseSettings = _secretService.GetSecretValue("FCMSettings");

            //    FirebaseApp.Create(new AppOptions()
            //    {
            //        Credential = GoogleCredential.FromJson(firebaseSettings)
            //        //Credential = GoogleCredential.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google-firebase.json"))
            //    });
            //}
        }

        public FirebaseService(IConfiguration configuration, SecretService secretService, ILogger<FirebaseService> logger)
        {
            _configuration = configuration;
            _secretService = secretService;
            _logger = logger;

            if (FirebaseApp.DefaultInstance == null)
            {
                string firebaseSettings = _secretService.GetSecretValue("FCMSettings");

                _logger.LogInformation(firebaseSettings);

                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromJson(firebaseSettings)
                    //Credential = GoogleCredential.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google-firebase.json"))
                });
            }
        }

        public string SendBackgroundPushNotification(string id, string token)
        {
            var message = new Message()
            {
                Token = token,
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>()
                    {
                        ["apns-push-type"] = "background",
                        ["apns-priority"] = "10"
                    },
                    Aps = new Aps
                    {
                        AlertString = "",
                        Badge = 0,
                        ContentAvailable = true
                    }
                },
                Android = new AndroidConfig()
                {
                    Priority = Priority.High
                },
                Data = new Dictionary<string, string>()
                {
                    ["id"] = id
                }
            };

            return FirebaseMessaging.DefaultInstance.SendAsync(message).Result;
        }

        public string SendPushNotification(string id, string token, string title, string body)
        {
            var message = new Message()
            {
                Token = token,
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>()
                    {
                        ["apns-push-type"] = "alert",
                        ["apns-priority"] = "10"
                    },
                    Aps = new Aps
                    {
                        Badge = 0,
                        ContentAvailable = true,
                        Sound = "default"
                    }
                },
                Android = new AndroidConfig()
                {
                    Priority = Priority.High
                },
                Notification = new Notification()
                {
                    Title = title,
                    Body = body
                },
                Data = new Dictionary<string, string>()
                {
                    ["id"] = id
                }
            };

            return FirebaseMessaging.DefaultInstance.SendAsync(message).Result;
        }
    }
}
