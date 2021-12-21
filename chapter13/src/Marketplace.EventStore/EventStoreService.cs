using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.Extensions.Hosting;

namespace Marketplace.EventStore
{
    public class EventStoreService : IHostedService
    {
        readonly EventStoreClient _esClient;
        readonly IEnumerable<SubscriptionManager> _subscriptionManagers;

        public EventStoreService(
            EventStoreClient esClient,
            IEnumerable<SubscriptionManager> subscriptionManagers)
        {
            _esClient = esClient;
            _subscriptionManagers = subscriptionManagers;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAll(
                _subscriptionManagers
                    .Select(projection => projection.Start())
            );
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscriptionManagers.ForEach(x => x.Stop());
            _esClient.Dispose();
            return Task.CompletedTask;
        }
    }
}