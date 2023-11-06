using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace WebAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string SendEmailVerification(string recipient, string verificationCode)
        {
            var senderEmailAddress = _configuration.GetValue<string>("EmailSenderAddress");

            return Send(senderEmailAddress, recipient, "Loop8 - Verify email address", $"Your email registration code is: {verificationCode}", null);
        }

        private string Send(string sender, string recipient, string subject, string htmlBody, string textBody)
        {
            // TODO: AWS region configuration
            using (var client = new AmazonSimpleEmailServiceClient())
            {
                var sendRequest = new SendEmailRequest
                {
                    Source = sender,
                    Destination = new Destination
                    {
                        ToAddresses =
                        new List<string> { recipient }
                    },
                    Message = new Message
                    {
                        Subject = new Content(subject),
                        Body = new Body
                        {
                            Html = new Content
                            {
                                Charset = "UTF-8",
                                Data = htmlBody
                            }
                            //,
                            //Text = new Content
                            //{
                            //    Charset = "UTF-8",
                            //    Data = textBody
                            //}
                        }
                    }
                };

                var result = client.SendEmailAsync(sendRequest).Result;
                return result.MessageId;                
            }
        }
    }
}
