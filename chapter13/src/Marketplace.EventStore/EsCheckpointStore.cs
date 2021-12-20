using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EventStore.Client;
using Marketplace.EventSourcing;
using EventData = EventStore.Client.EventData;
using ResolvedEvent = EventStore.Client.ResolvedEvent;
using StreamMetadata = EventStore.Client.StreamMetadata;
using StreamPosition = EventStore.Client.StreamPosition;

namespace Marketplace.EventStore
{
    public class EsCheckpointStore : ICheckpointStore
    {
        const string CheckpointStreamPrefix = "checkpoint:";
        readonly EventStoreClient _client;
        readonly string _streamName;

        public EsCheckpointStore(
            EventStoreClient client,
            string subscriptionName)
        {
            _client = client;
            _streamName = CheckpointStreamPrefix + subscriptionName;
        }

        public async Task<ulong?> GetCheckpoint()
        {
            await using var readResult = _client.ReadStreamAsync(
                Direction.Backwards,
                _streamName,
                StreamPosition.End,
                1
            );

            var eventData = await readResult
                .FirstOrDefaultAsync();
            
            if (eventData.Equals(default(ResolvedEvent)))
            {
                await StoreCheckpoint(null);
                await SetStreamMaxCount();
                return null;
            }

            return eventData.Deserialize<Checkpoint>()?.Position;
        }

        public Task StoreCheckpoint(ulong? checkpoint)
        {
            var @event = new Checkpoint {Position = checkpoint};

            var preparedEvent =
                new EventData(
                    Uuid.NewUuid(),
                    "$checkpoint",
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(@event)
                    )
                );

            return _client.AppendToStreamAsync(
                _streamName,
                StreamState.Any,
                new []{ preparedEvent }
            );
        }

        async Task SetStreamMaxCount()
        {
            var result = await _client.GetStreamMetadataAsync(_streamName);

            if (!result.Metadata.MaxCount.HasValue)
                await _client.SetStreamMetadataAsync(
                    _streamName, StreamState.Any,
                    new StreamMetadata(1)
                );
        }

        class Checkpoint
        {
            public ulong? Position { get; set; }
        }
    }
}