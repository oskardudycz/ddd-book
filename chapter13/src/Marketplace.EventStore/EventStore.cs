using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Client;
using Marketplace.EventSourcing;
using Newtonsoft.Json;

namespace Marketplace.EventStore
{
    public class EventStore : IEventStore
    {
        readonly EventStoreClient _client;

        public EventStore(EventStoreClient client)
            => _client = client;

        public Task AppendEvents(
            string streamName,
            long version,
            params object[] events
        )
        {
            if (events == null || !events.Any()) return Task.CompletedTask;

            var preparedEvents = events
                .Select(
                    @event =>
                        new EventData(
                            Uuid.NewUuid(), 
                            TypeMapper.GetTypeName(@event.GetType()),
                            Serialize(@event),
                            Serialize(
                                new EventMetadata
                                {
                                    ClrType = @event.GetType().FullName
                                }
                            )
                        )
                )
                .ToArray();

            return _client.AppendToStreamAsync(
                streamName,
                StreamRevision.FromInt64(version),
                preparedEvents
            );

            static byte[] Serialize(object data)
                => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
        }

        public Task AppendEvents(
            string streamName,
            params object[] events
        )
            => AppendEvents(streamName, StreamState.Any, events);

        public async Task<IEnumerable<object>> LoadEvents(string stream)
        {
            await using var readResult = _client.ReadStreamAsync(
                Direction.Forwards,
                stream,
                StreamPosition.Start
            );

            if(await readResult.ReadState != ReadState.Ok)
                throw new ArgumentOutOfRangeException(
                    nameof(stream), $"Stream '{stream}' was not found"
                );

            return await readResult
                .Select(@event => @event.Deserialize())
                .ToListAsync();
        }

        public async Task<bool> StreamExists(string stream)
        {
            await using var readResult = _client.ReadStreamAsync(
                Direction.Forwards,
                stream,
                StreamPosition.Start
            );

            return await readResult.ReadState == ReadState.Ok;
        }
    }
}
