using System;
using EventStore.Client;
using Marketplace.EventSourcing;
using Marketplace.EventStore;
using Marketplace.RavenDb;
using Marketplace.Users.Auth;
using Marketplace.Users.Domain.Shared;
using Marketplace.Users.Projections;
using Marketplace.Users.UserProfiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using static Marketplace.Users.Projections.ReadModels;

namespace Marketplace.Users
{
    public static class UsersModule
    {
        const string SubscriptionName = "usersSubscription";

        public static IMvcCoreBuilder AddUsersModule(
            this IMvcCoreBuilder builder,
            string databaseName,
            CheckTextForProfanity profanityCheck
        )
        {
            EventMappings.MapEventTypes();

            builder.Services.AddSingleton(
                    c =>
                        new UserProfileCommandService(
                            c.GetAggregateStore(),
                            profanityCheck,
                            c.GetRequiredService<ILogger<UserProfileCommandService>>()
                        )
                )
                .AddSingleton<GetUsersModuleSession>(
                    c =>
                    {
                        var store = c.GetRequiredService<IDocumentStore>();
                        store.CheckAndCreateDatabase(databaseName);

                        IAsyncDocumentSession GetSession()
                            => store.OpenAsyncSession(databaseName);

                        return GetSession;
                    }
                )
                .AddSingleton(
                    c =>
                    {
                        var getSession =
                            c.GetRequiredService<GetUsersModuleSession>();

                        return new SubscriptionManager(
                            c.GetEventStoreClient(),
                            new RavenDbCheckpointStore(
                                () => getSession(),
                                SubscriptionName
                            ),
                            SubscriptionName,
                            StreamName.AllStream,
                            c.GetRequiredService<ILogger<SubscriptionManager>>(),
                            new RavenDbProjection<UserDetails>(
                                () => getSession(),
                                UserDetailsProjection.GetHandler,
                                c.GetRequiredService<ILogger<RavenDbProjection<UserDetails>>>()
                            )
                        );
                    }
                )
                .AddSingleton<AuthService>();

            builder.AddApplicationPart(typeof(UsersModule).Assembly);

            return builder;
        }
        
        static EventStoreClient GetEventStoreClient(
            this IServiceProvider provider
        )
            => provider.GetRequiredService<EventStoreClient>();

        static IAggregateStore GetAggregateStore(this IServiceProvider provider)
            => provider.GetRequiredService<IAggregateStore>();
    }

    public delegate IAsyncDocumentSession GetUsersModuleSession();
}
