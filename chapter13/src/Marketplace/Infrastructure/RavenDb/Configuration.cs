using Raven.Client.Documents;

namespace Marketplace.Infrastructure.RavenDb
{
    public static class Configuration
    {
        public static IDocumentStore ConfigureRavenDb(
            string serverUrl
        )
        {
            var store = new DocumentStore
            {
                Urls = new[] {serverUrl}
            };
            store.Initialize();

            return store;
        }
    }
}