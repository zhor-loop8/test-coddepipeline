using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace WebAPI.Services
{
    public class MessageService
    {
        private readonly IConfiguration _configuration;

        public MessageService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //public string GetUserPairingInvitationMessage(string inviterName, string customMessage)
        //{
        //    if (!string.IsNullOrWhiteSpace(customMessage))
        //    {
        //        return customMessage;
        //    }
        //    else if(!string.IsNullOrWhiteSpace(inviterName))
        //    {
        //        return $"{inviterName} is inviting you to be a Loop8 trusted contact.";
        //    }
        //    else
        //    {
        //        return "A user is inviting you to be a Loop8 trusted contact.";
        //    }
        //}
    }
}
