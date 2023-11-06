using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using System.Collections.Generic;
using static Google.Apis.Requests.BatchRequest;
using WebAPI.DataModels;
using Microsoft.AspNetCore.Mvc;
using System;

namespace WebAPI.Services
{
    public class PaymentService
    {
        private readonly IConfiguration _configuration;

        public PaymentService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public bool CreateSubscription(PaymentCard pc)
        {
            var guid = Guid.NewGuid();
            // craeting payment method
            var paymentMethodOptions = new PaymentMethodCreateOptions
            {
                BillingDetails = new PaymentMethodBillingDetailsOptions
                {
                    Name = "user-" + guid,
                },
                Card = new PaymentMethodCardOptions
                {
                    Cvc = pc.Cvc,
                    ExpMonth = pc.ExpMonth,
                    ExpYear = pc.ExpYear,
                    Number = pc.Number,
                },
                Type = "card",
            };

            var paymentMethodService = new PaymentMethodService();
            var paymentMethodResult = paymentMethodService.Create(paymentMethodOptions);

            string stripeCustomerId = null;

            //creating customer
            var customerOptions = new CustomerCreateOptions
            {
                Name = "user-" + guid,
                PaymentMethod = paymentMethodResult.Id,
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = paymentMethodResult.Id,

                },
                Email = "zhora@l8.dev",

            };
            var customerService = new CustomerService();
            var customerResult = customerService.Create(customerOptions);
            stripeCustomerId = customerResult.Id;


            

            // create subscription
            var subscriptionOptions = new SubscriptionCreateOptions
            {
                Customer = stripeCustomerId,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = "price_1NevB6Ee6PQC8Mmb4zTU92G9",
                    },
                },
            };

            var subscriptionService = new SubscriptionService();
            var subscriptionResult = subscriptionService.Create(subscriptionOptions);

            return true;

        }
    }
}
