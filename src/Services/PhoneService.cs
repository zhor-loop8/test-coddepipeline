using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.Pinpoint;
using Amazon.Pinpoint.Model;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;

namespace WebAPI.Services
{
    public class PhoneService
    {
        private readonly IConfiguration _configuration;
        
        //add these to a configuration file
        private readonly string region = "us-west-2";
        private readonly string appId = "852490ba0abe4cd19c6d32b71fec724d";
        private readonly string message = "Your L8P8 registration code is: ";
        private readonly string originationNumber = "+18449395296";
        private readonly string senderId = "L8P8";
        private readonly string registeredKeyword = "KEYWORD_110896062996";

        public PhoneService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendPhoneVerification(string recipient, string phoneVerificationToken)
        {
            //check if the recipient is a valid phone number

            //send the message
            using (AmazonPinpointClient client = new AmazonPinpointClient(RegionEndpoint.GetBySystemName(region)))
            {
                SendMessagesRequest sendRequest = new SendMessagesRequest
                {
                    ApplicationId = appId,
                    MessageRequest = new MessageRequest
                    {
                        Addresses = new Dictionary<string, AddressConfiguration>
                        {
                            {
                                recipient,
                                new AddressConfiguration { ChannelType = "SMS"}
                            }
                        },
                        MessageConfiguration = new DirectMessageConfiguration
                        {
                            SMSMessage = new SMSMessage
                            {
                                Body = message + phoneVerificationToken,
                                MessageType = MessageType.TRANSACTIONAL,
                                OriginationNumber = originationNumber,
                                SenderId = senderId,
                                Keyword = registeredKeyword
                            }
                        }
                    }
                };

                SendMessagesResponse response = client.SendMessagesAsync(sendRequest).Result;
            }
        }

        public void SendSMS(string recipient, string message)
        {
            using (AmazonPinpointClient client = new AmazonPinpointClient(RegionEndpoint.GetBySystemName(region)))
            {
                var validationResult = client.PhoneNumberValidateAsync(new PhoneNumberValidateRequest() { NumberValidateRequest = new NumberValidateRequest() { PhoneNumber = recipient } }).Result;

                if (validationResult.NumberValidateResponse.PhoneType != "MOBILE")
                    throw new ApplicationException($"PHONE NUMBER VALIDATION FAILED: {validationResult.NumberValidateResponse.PhoneType}");

                SendMessagesRequest sendRequest = new SendMessagesRequest
                {
                    ApplicationId = appId,
                    MessageRequest = new MessageRequest
                    {
                        Addresses = new Dictionary<string, AddressConfiguration>
                        {
                            {
                                recipient,
                                new AddressConfiguration { ChannelType = "SMS"}
                            }
                        },
                        MessageConfiguration = new DirectMessageConfiguration
                        {
                            SMSMessage = new SMSMessage
                            {
                                Body = message,
                                MessageType = MessageType.TRANSACTIONAL,
                                OriginationNumber = originationNumber,
                                SenderId = senderId,
                                Keyword = registeredKeyword
                            }
                        }
                    }
                };

                SendMessagesResponse response = client.SendMessagesAsync(sendRequest).Result;

                // return ?
            }
        }

    }
}
