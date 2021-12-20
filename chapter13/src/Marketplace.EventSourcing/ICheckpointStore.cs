using System.Threading.Tasks;

namespace Marketplace.EventSourcing
{
    public interface ICheckpointStore
    {
        Task<ulong?> GetCheckpoint();
        Task StoreCheckpoint(ulong? checkpoint);
    }
}