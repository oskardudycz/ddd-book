using System.Text;
using EventStore.Client;
using Marketplace.EventSourcing;

namespace Marketplace.EventStore
{
    public static class EventDeserializer
    {
        public static object Deserialize(this ResolvedEvent resolvedEvent)
        {
            var dataType = TypeMapper.GetType(resolvedEvent.Event.EventType);
            var jsonData = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);
            var data = System.Text.Json.JsonSerializer.Deserialize(jsonData, dataType);
            return data;
        }

        public static T Deserialize<T>(this ResolvedEvent resolvedEvent)
        {
            var jsonData = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);
            return System.Text.Json.JsonSerializer.Deserialize<T>(jsonData);
        }
    }
}