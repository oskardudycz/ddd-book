using System;
using System.Threading.Tasks;
using Marketplace.EventSourcing;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents.Session;

namespace Marketplace.RavenDb
{
    public class RavenDbProjection<T> : ISubscription
    {
        static readonly string ReadModelName = typeof(T).Name;

        public RavenDbProjection(
            GetSession getSession,
            Projector projector,
            ILogger<RavenDbProjection<T>> logger)
        {
            _projector = projector;
            _logger    = logger;
            GetSession = getSession;
        }

        GetSession GetSession { get; }
        readonly Projector _projector;
        readonly ILogger<RavenDbProjection<T>> _logger;

        public async Task Project(object @event)
        {
            using var session = GetSession();

            var handler = _projector(session, @event);

            if ( handler == null) return;

            _logger.LogDebug("Projecting {Event} to {Model}", @event, ReadModelName);

            await handler();
            await session.SaveChangesAsync();
        }

        public delegate Func<Task> Projector(
            IAsyncDocumentSession session,
            object @event
        );
    }
}