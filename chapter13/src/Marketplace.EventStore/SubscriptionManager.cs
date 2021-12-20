using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Marketplace.EventSourcing;
using Microsoft.Extensions.Logging;

namespace Marketplace.EventStore
{
    public class SubscriptionManager
    {
        readonly ICheckpointStore _checkpointStore;
        readonly string _name;
        readonly StreamName _streamName;
        readonly IEventStoreConnection _connection;
        readonly ISubscription[] _subscriptions;
        EventStoreCatchUpSubscription _subscription;
        readonly ILogger<SubscriptionManager> _logger;
        bool _isAllStream;

        public SubscriptionManager(
            IEventStoreConnection connection,
            ICheckpointStore checkpointStore,
            string name,
            StreamName streamName,
            ILogger<SubscriptionManager> logger,
            params ISubscription[] subscriptions
        )
        {
            _connection = connection;
            _checkpointStore = checkpointStore;
            _name = name;
            _streamName = streamName;
            _subscriptions = subscriptions;
            _isAllStream = streamName.IsAllStream;
            _logger = logger;
        }

        public async Task Start()
        {
            var settings = new CatchUpSubscriptionSettings(
                2000, 500,
                _logger.IsEnabled(LogLevel.Debug),
                false, _name
            );

            _logger.LogDebug("Starting the projection manager...");

            var position = await _checkpointStore.GetCheckpoint();

            _logger.LogDebug(
                "Retrieved the checkpoint: {Checkpoint}", position
            );

            _subscription = _isAllStream
                ? (EventStoreCatchUpSubscription)
                _connection.SubscribeToAllFrom(
                    GetAllStreamPosition(),
                    settings,
                    EventAppeared,
                    LiveProcessingStarted,
                    SubscriptionDropped
                )
                : _connection.SubscribeToStreamFrom(
                    _streamName,
                    GetStreamPosition(),
                    settings,
                    EventAppeared,
                    LiveProcessingStarted,
                    SubscriptionDropped
                );

            _logger.LogDebug("Subscribed to $all stream");

            Position? GetAllStreamPosition()
                => position.HasValue
                    ? new Position(position.Value, position.Value)
                    : AllCheckpoint.AllStart;

            long? GetStreamPosition()
                => position ?? StreamCheckpoint.StreamStart;
        }

        async Task EventAppeared(
            EventStoreCatchUpSubscription _,
            ResolvedEvent resolvedEvent
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
                    // ReSharper disable once PossibleInvalidOperationException
                    _isAllStream
                        ? resolvedEvent.OriginalPosition.Value.CommitPosition
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

        private void LiveProcessingStarted(
            EventStoreCatchUpSubscription subscription)
            => _logger.LogDebug(
                "Subscription {SubscriptionName} has caught up, now processing live",
                _name
            );

        private void SubscriptionDropped(
            EventStoreCatchUpSubscription subscription,
            SubscriptionDropReason reason,
            Exception exc)
            => _logger.LogError(
                "Projection {SubscriptionName} dropped with {DropReason}, Exception: {ExceptionMessage}",
                _name, reason, $"{exc.Message} {exc.StackTrace}"
            );

        public void Stop() => _subscription.Stop();
    }
}