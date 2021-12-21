using System;
using EventStore.Client;
using Marketplace.Ads.Integration.ClassifiedAds;
using Marketplace.EventSourcing;
using Marketplace.EventStore;
using Marketplace.RavenDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Marketplace.Ads.Integration
{
    public static class AdsIntegrationModule
    {
        const string SubscriptionName = "adsIntegrationSubscription";

        public static IMvcCoreBuilder AddAdsModule(
            this IMvcCoreBuilder builder,
            string databaseName
        )
        {
            EventMappings.MapEventTypes();

            builder.Services.AddSingleton(
                c =>
                {
                    var store = c.GetRavenStore();
                    store.CheckAndCreateDatabase(databaseName);

                    IAsyncDocumentSession GetSession()
                        => c.GetRavenStore()
                            .OpenAsyncSession(databaseName);

                    var client = c.GetEventStoreClient();
                    var eventStore = c.GetEventStore();

                    return new SubscriptionManager(
                        client,
                        new EsCheckpointStore(
                            client, SubscriptionName
                        ),
                        SubscriptionName,
                        StreamName.AllStream,
                        c.GetRequiredService<ILogger<SubscriptionManager>>(),
                        new EventStoreReactor(
                            c.GetRequiredService<ILogger<EventStoreReactor>>(),
                            e => AdsReaction.React(eventStore, GetSession, e)
                        )
                    );
                }
            );

            return builder;
        }

        static IDocumentStore GetRavenStore(
            this IServiceProvider provider
        )
            => provider.GetRequiredService<IDocumentStore>();

        static IEventStore GetEventStore(
            this IServiceProvider provider
        )
            => provider.GetRequiredService<IEventStore>();

        static EventStoreClient GetEventStoreClient(
            this IServiceProvider provider
        )
            => provider.GetRequiredService<EventStoreClient>();
    }
}
