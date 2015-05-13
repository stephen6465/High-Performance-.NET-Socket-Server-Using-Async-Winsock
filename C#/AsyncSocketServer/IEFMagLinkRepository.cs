using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace New_MagLink
{
    public interface IEFMagLinkRepository : IDisposable
    {
        Task<IEnumerable<AckMessage>> GetAckMessageAsync();
        Task<Registry> GetRegistryAsync();
        Task CreateRegistryAsync(Registry registry);
      Task CreateMhistoryAsync(String message);
        Task<Queue> CreateQueueRecordAsync(String message);
        Task SaveChangesQueueAsync(Queue queue);
      Task SaveChangesMhistoryAsync(Message_History mhistory);
      Task ProcessQueueAsync(String messageID);
     Task<IEnumerable<Queue>> QueueToSendAsync();
    Task CreateAckRecordAsync(String m);
    Task ClearQueueAsync();
   Task<Queue> GetQueueAsync(Int64 ID);

    }
}
