﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using ProfileAPI.Models;
using System.IdentityModel.Tokens.Jwt;

namespace ProfileAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // This is required to be instantiated before the OpenIdConnectOptions starts getting configured.
            // By default, the claims mapping will map claim names in the old format to accommodate older SAML applications.
            // 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' instead of 'roles'
            // This flag ensures that the ClaimsIdentity claims collection will be built from the claims in the token
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(options =>
                {
                    Configuration.Bind("AzureAd", options);
                    options.Events = new JwtBearerEvents();

                    /// <summary>
                    /// Below you can do extended token validation and check for additional claims, such as:
                    ///
                    /// - check if the caller's tenant is in the allowed tenants list via the 'tid' claim (for multi-tenant applications)
                    /// - check if the caller's account is homed or guest via the 'acct' optional claim
                    /// - check if the caller belongs to right roles or groups via the 'roles' or 'groups' claim, respectively
                    ///
                    /// Bear in mind that you can do any of the above checks within the individual routes and/or controllers as well.
                    /// For more information, visit: https://docs.microsoft.com/azure/active-directory/develop/access-tokens#validate-the-user-has-permission-to-access-this-data
                    /// </summary>

                    //options.Events.OnTokenValidated = async context =>
                    //{
                    //    string[] allowedClientApps = { /* list of client ids to allow */ };

                    //    string clientappId = context?.Principal?.Claims
                    //        .FirstOrDefault(x => x.Type == "azp" || x.Type == "appid")?.Value;

                    //              if (!allowedClientApps.Contains(clientappId))
                    //              {
                    //                  throw new UnauthorizedAccessException("The client app is not permitted to access this API");
                    //              }

                    //              await Task.CompletedTask;
                    //};
                }, options =>
                {
                    Configuration.Bind("AzureAd", options);
                })
                .EnableTokenAcquisitionToCallDownstreamApi(options => Configuration.Bind("AzureAd", options))
                .AddMicrosoftGraph(Configuration.GetSection("DownstreamAPI"))
                .AddInMemoryTokenCaches();

            services.AddDbContext<ProfileContext>(opt => opt.UseInMemoryDatabase("Profile"));

            services.AddControllers();

            // The following flag can be used to get more descriptive errors in development environments
            // Enable diagnostic logging to help with troubleshooting. For more details, see https://aka.ms/IdentityModel/PII.
            // You might not want to keep this following flag on for production
            IdentityModelEventSource.ShowPII = true;

            // Allowing CORS for all domains and HTTP methods for the purpose of the sample
            // In production, modify this with the actual domains and HTTP methods you want to allow
            services.AddCors(o => o.AddPolicy("default", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders("WWW-Authenticate"); // expose header to receive claim challenges
            }));

            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {

                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description =
                        "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                // Since IdentityModel version 5.2.1 (or since Microsoft.AspNetCore.Authentication.JwtBearer version 2.2.0),
                // PII hiding in log files is enabled by default for GDPR concerns.
                // For debugging/development purposes, one can enable additional detail in exceptions by setting IdentityModelEventSource.ShowPII to true.
                IdentityModelEventSource.ShowPII = true;

                app.UseDeveloperExceptionPage();

                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseCors("default");
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
