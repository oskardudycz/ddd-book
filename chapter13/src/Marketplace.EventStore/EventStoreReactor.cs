using System;
using System.Linq;
using System.Threading.Tasks;
using Marketplace.EventSourcing;
using Microsoft.Extensions.Logging;

namespace Marketplace.EventStore
{
    public class EventStoreReactor : ISubscription
    {
        public EventStoreReactor(
            ILogger<EventStoreReactor> logger,
            params Reactor[] reactions)
        {
            this.logger = logger;
            _reactions = reactions;
        }

        readonly Reactor[] _reactions;
        readonly ILogger<EventStoreReactor> logger;

        public Task Project(object @event)
        {
            var handlers = _reactions.Select(x => x(@event))
                .Where(x => x != null)
                .ToArray();

            if (!handlers.Any()) return Task.CompletedTask;

            logger.LogDebug("Reacting to event {Event}", @event);

            return Task.WhenAll(handlers.Select(x => x()));
        }
    }

    public delegate Func<Task> Reactor(object @event);
}
