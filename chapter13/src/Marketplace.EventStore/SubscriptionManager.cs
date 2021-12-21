using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using Marketplace.EventSourcing;
using Microsoft.Extensions.Logging;

namespace Marketplace.EventStore
{
    public class SubscriptionManager
    {
        readonly ICheckpointStore _checkpointStore;
        readonly string _name;
        readonly StreamName _streamName;
        readonly EventStoreClient _client;
        readonly ISubscription[] _subscriptions;
        StreamSubscription _subscription;
        readonly ILogger<SubscriptionManager> _logger;
        bool _isAllStream;

        public SubscriptionManager(
            EventStoreClient client,
            ICheckpointStore checkpointStore,
            string name,
            StreamName streamName,
            ILogger<SubscriptionManager> logger,
            params ISubscription[] subscriptions
        )
        {
            _client = client;
            _checkpointStore = checkpointStore;
            _name = name;
            _streamName = streamName;
            _subscriptions = subscriptions;
            _isAllStream = streamName.IsAllStream;
            _logger = logger;
        }

        public async Task Start()
        {
            _logger.LogDebug("Starting the projection manager...");

            var position = await _checkpointStore.GetCheckpoint();

            _logger.LogDebug(
                "Retrieved the checkpoint: {Checkpoint}", position
            );

            _subscription = _isAllStream
                ? await SubscribeToAll()
                : await SubscribeToStream();

            _logger.LogDebug("Subscribed to $all stream");

            Task<StreamSubscription> SubscribeToAll()
            {
                if (position.HasValue)
                {
                    return _client.SubscribeToAllAsync(
                        new Position(position.Value, position.Value),
                        EventAppeared,
                        false,
                        SubscriptionDropped
                    );
                }

                return _client.SubscribeToAllAsync(
                    EventAppeared,
                    false,
                    SubscriptionDropped
                );
            }

            Task<StreamSubscription> SubscribeToStream()
            {
                if (position.HasValue)
                {
                    return _client.SubscribeToStreamAsync(
                        _streamName,
                        StreamPosition.FromInt64((long)position.Value),
                        EventAppeared,
                        false,
                        SubscriptionDropped
                    );
                }

                return _client.SubscribeToStreamAsync(
                    _streamName,
                    EventAppeared,
                    false,
                    SubscriptionDropped
                );
            }
        }

        async Task EventAppeared(
            StreamSubscription _,
            ResolvedEvent resolvedEvent,
            CancellationToken ct
        )
        {
            if (resolvedEvent.Event.EventType.StartsWith("$")) return;

            object @event = null;

            try
            {
                @event = resolvedEvent.Deserialize();

                _logger.LogDebug("Projecting event {Event}", @event.ToString());

                await Task.WhenAll(
                    _subscriptions.Select(x => x.Project(@event))
                );

                await _checkpointStore.StoreCheckpoint(
                    _isAllStream
                        ? resolvedEvent.OriginalPosition!.Value.CommitPosition
                        : resolvedEvent.Event.EventNumber
                );
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Error occured when projecting the event {Event}",
                    @event ?? resolvedEvent
                );
                throw;
            }
        }

        private void SubscriptionDropped(
            StreamSubscription subscription,
            SubscriptionDroppedReason reason,
            Exception exc)
            => _logger.LogError(
                "Projection {SubscriptionName} dropped with {DropReason}, Exception: {ExceptionMessage}",
                _name, reason, $"{exc.Message} {exc.StackTrace}"
            );

        public void Stop() => _subscription.Dispose();
    }
}