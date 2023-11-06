using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.DataModels;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI
{
    public class Startup
    {
        public IConfiguration _configuration { get; }

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddMemoryCache();
            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebAPI", Version = "v1" });

                c.AddSecurityDefinition("Token", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter authorization token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Token"
                            }
                        },
                        new string[]{}
                    }
                });
            });

            //services.AddDbContext<AppDbContext>(c =>
            //{
            //    c.UseNpgsql(Configuration.GetConnectionString("l8p8-postgres-db")); //.EnableSensitiveDataLogging(); 
            //});

            services.AddDbContext<l8p8Context>();

            //TODO: fix datetime mapping between .NET and Postgres 
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            //TODO: refactor
            var _secretService = new SecretService();
            RedisConnectionSecret connectionSecret = JsonConvert.DeserializeObject<RedisConnectionSecret>(_secretService.GetSecretValue(_configuration.GetValue<string>("RedisConnectionSecretName")));

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
                {
                    EndPoints = { connectionSecret.Endpoint },
                    Ssl = connectionSecret.Ssl,
                    Password = connectionSecret.Key,
                };
            });

            services.AddTransient<MessageService>();
            services.AddTransient<FirebaseService>();
            services.AddTransient<EmailService>();
            services.AddTransient<PhoneService>();
            services.AddTransient<SettingService>();
            services.AddTransient<SecretService>();
            services.AddTransient<StorageService>();
            services.AddTransient<Services.EventService>();
            services.AddTransient<PaymentService>();
            services.AddTransient<UserPairingService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebAPI v1"));

            app.UseRouting();

            // Enable automatic tracing integration.
            app.UseSentryTracing();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //string stripeSecretKey = _configuration.GetValue<string>("StripeSecretKey");
            //StripeConfiguration.ApiKey = stripeSecretKey;
        }
    }
}
