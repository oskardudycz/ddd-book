using EventStore.ClientAPI;
using Marketplace.Ads;
using Marketplace.EventSourcing;
using Marketplace.EventStore;
using Marketplace.Infrastructure.Currency;
using Marketplace.Infrastructure.Profanity;
using Marketplace.Infrastructure.Vue;
using Marketplace.Modules.Images;
using Marketplace.PaidServices;
using Marketplace.Users;
using Marketplace.WebApi;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using static Marketplace.Infrastructure.RavenDb.Configuration;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

// ReSharper disable UnusedMember.Global

[assembly: ApiController]

namespace Marketplace
{
    public class Startup
    {
        public const string CookieScheme = "MarketplaceScheme";

        public Startup(
            IHostingEnvironment environment,
            IConfiguration configuration
        )
        {
            Environment   = environment;
            Configuration = configuration;
        }

        IConfiguration Configuration { get; }
        IHostingEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var esConnection = EventStoreConnection.Create(
                Configuration["eventStore:connectionString"],
                ConnectionSettings.Create().KeepReconnecting(),
                Environment.ApplicationName
            );
            var eventStore = new EventStore.EventStore(esConnection);
            var purgomalumClient = new PurgomalumClient();

            var documentStore = ConfigureRavenDb(
                Configuration["ravenDb:server"]
            );

            services.AddCors();
            
            services.AddSingleton(new ImageQueryService(ImageStorage.GetFile));
            services.AddSingleton(esConnection);

            services.AddSingleton<IEventStore>(eventStore);

            services.AddSingleton<IAggregateStore>(sp =>
                new EsAggregateStore(eventStore, sp.GetRequiredService<ILogger<EsAggregateStore>>())
            );
            services.AddSingleton(documentStore);

            services.AddHostedService<EventStoreService>();

            services
                .AddAuthentication(
                    CookieAuthenticationDefaults.AuthenticationScheme
                )
                .AddCookie();

            services
                .AddMvcCore(
                    options =>
                    {
                        // options.Conventions.Add(new CommandConvention());
                        // TODO: Get rid of that: https://stackoverflow.com/questions/57684093/using-usemvc-to-configure-mvc-is-not-supported-while-using-endpoint-routing
                        options.EnableEndpointRouting = false;
                    }
                )
                .AddApplicationPart(GetType().Assembly)
                .AddAdsModule(
                    "ClassifiedAds",
                    new FixedCurrencyLookup(),
                    ImageStorage.UploadFile
                )
                .AddUsersModule(
                    "Users",
                    purgomalumClient.CheckForProfanity
                )
                .AddPaidServicesModule("PaidServices")
                .AddApiExplorer()  
                .AddNewtonsoftJson(o =>
                {
                    o.SerializerSettings.Converters.Add(new StringEnumConverter());
                    o.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });

            services.AddSpaStaticFiles(
                configuration =>
                    configuration.RootPath = "ClientApp/dist"
            );

            services.AddSwaggerGen(
                c =>
                    c.SwaggerDoc(
                        "v1",
                        new OpenApiInfo
                        {
                            Title = "ClassifiedAds", Version = "v1"
                        }
                    )
            );
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                // TODO: fix that to allow only SPA url
                app.UseCors(
                    options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                );
            }

            app.UseAuthentication();
            
            app.UseMvc(
                routes =>
                {
                    routes.MapRoute(
                        "default",
                        "{controller=Home}/{action=Index}/{id?}"
                    );

                    routes.MapRoute(
                        "api",
                        "api/{controller=Home}/{action=Index}/{id?}"
                    );

                    routes.MapSpaFallbackRoute(
                        "spa-fallback",
                        new {controller = "Home", action = "Index"}
                    );
                }
            );

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseSwagger();

            app.UseSwaggerUI(
                c => c.SwaggerEndpoint(
                    "/swagger/v1/swagger.json", "ClassifiedAds v1"
                )
            );

            app.UseSpa(
                spa =>
                {
                    spa.Options.SourcePath = "ClientApp";

                    if (env.IsDevelopment())
                        spa.UseVueDevelopmentServer("serve:bs");
                }
            );
        }
    }
}
