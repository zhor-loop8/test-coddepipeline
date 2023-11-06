using Amazon.SimpleEmail.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe.Tax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddAWSProvider();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();

                    webBuilder.ConfigureAppConfiguration((context, config) =>
                    {
                        webBuilder.UseSentry(o =>
                        {
                            o.Dsn = "https://2dab139dc76172c00f461d9eca7290be@o4505752050401280.ingest.sentry.io/4505791858671616";
                            // When configuring for the first time, to see what the SDK is doing:
                            o.Debug = true;
                            // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring. We recommend adjusting this value in production.
                            o.TracesSampleRate = 1.0;
                            // Environment
                            o.Environment = config.Build().GetValue<string>("Environment").ToLower();
                        });
                    });

                });
    }
}
