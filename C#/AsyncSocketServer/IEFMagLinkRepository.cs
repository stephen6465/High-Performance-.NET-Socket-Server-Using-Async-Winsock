using System;
using System.Collections.Generic;

namespace New_MagLink
{
    public interface IEFMagLinkRepository : IDisposable
    {
        IEnumerable<AckMessage> GetAckMessage();
        Registry GetRegistry();
        void CreateRegistry(Registry registry);
        void CreateMhistory(String message);
        Queue CreateQueueRecord(String message);
        void SaveChanges();
        void ProcessQueue(String messageID);
        IEnumerable<Queue> QueueToSend();
    }
}
