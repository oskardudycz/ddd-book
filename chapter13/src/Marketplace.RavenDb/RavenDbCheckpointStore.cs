using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Marketplace.EventSourcing;
using Raven.Client.Documents.Session;

namespace Marketplace.RavenDb
{
    public class RavenDbCheckpointStore : ICheckpointStore
    {
        readonly string _checkpointName;
        readonly Func<IAsyncDocumentSession> _getSession;

        public RavenDbCheckpointStore(
            Func<IAsyncDocumentSession> getSession,
            string checkpointName)
        {
            _getSession = getSession;
            _checkpointName = checkpointName;
        }

        public async Task<ulong?> GetCheckpoint()
        {
            using var session = _getSession();

            var checkpoint = await session.LoadAsync<Checkpoint>(_checkpointName);
            return checkpoint?.Position ?? (ulong?)AllCheckpoint.AllStart?.CommitPosition;
        }

        public async Task StoreCheckpoint(ulong? position)
        {
            using var session = _getSession();

            var checkpoint = await session.LoadAsync<Checkpoint>(_checkpointName);

            if (checkpoint == null)
            {
                checkpoint = new Checkpoint
                {
                    Id = _checkpointName
                };
                await session.StoreAsync(checkpoint);
            }

            checkpoint.Position = position;
            await session.SaveChangesAsync();
        }

        class Checkpoint
        {
            public string Id { get; set; }
            public ulong? Position { get; set; }
        }
    }
}