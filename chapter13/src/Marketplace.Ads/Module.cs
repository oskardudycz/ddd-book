using System;
using EventStore.ClientAPI;
using Marketplace.Ads.ClassifiedAds;
using Marketplace.Ads.Domain.Shared;
using Marketplace.Ads.Queries.Projections;
using Marketplace.EventSourcing;
using Marketplace.EventStore;
using Marketplace.RavenDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using static Marketplace.Ads.Queries.Projections.ReadModels;

namespace Marketplace.Ads
{
    public static class AdsModule
    {
        const string SubscriptionName = "adsSubscription";

        public static IMvcCoreBuilder AddAdsModule(
            this IMvcCoreBuilder builder,
            string databaseName,
            ICurrencyLookup currencyLookup,
            UploadFile uploadFile
        )
        {
            EventMappings.MapEventTypes();
            
            builder.Services.AddSingleton(
                c =>
                    new ClassifiedAdsCommandService(
                        c.GetAggregateStore(),
                        currencyLookup,
                        uploadFile,
                        c.GetRequiredService<ILogger<ClassifiedAdsCommandService>>()
                    )
            );

            builder.Services.AddSingleton(
                c =>
                {
                    var store = c.GetRavenStore();
                    store.CheckAndCreateDatabase(databaseName);
                    
                    IAsyncDocumentSession GetSession()
                        => c.GetRavenStore()
                            .OpenAsyncSession(databaseName);

                    return new SubscriptionManager(
                        c.GetEsConnection(),
                        new RavenDbCheckpointStore(
                            GetSession, SubscriptionName
                        ),
                        SubscriptionName,
                        StreamName.AllStream,
                        c.GetRequiredService<ILogger<SubscriptionManager>>(),
                        new RavenDbProjection<ClassifiedAdDetails>(
                            GetSession,
                            ClassifiedAdDetailsProjection.GetHandler,
                            c.GetRequiredService<ILogger<RavenDbProjection<ClassifiedAdDetails>>>()
                        ),
                        new RavenDbProjection<MyClassifiedAds>(
                            GetSession,
                            MyClassifiedAdsProjection.GetHandler,
                            c.GetRequiredService<ILogger<RavenDbProjection<MyClassifiedAds>>>()
                        )
                    );
                }
            );

            builder.AddApplicationPart(typeof(AdsModule).Assembly);

            return builder;
        }

        static IDocumentStore GetRavenStore(
            this IServiceProvider provider
        )
            => provider.GetRequiredService<IDocumentStore>();
        
        static IEventStoreConnection GetEsConnection(
            this IServiceProvider provider
        )
            => provider.GetRequiredService<IEventStoreConnection>();

        static IAggregateStore GetAggregateStore(this IServiceProvider provider)
            => provider.GetRequiredService<IAggregateStore>();
    }
}