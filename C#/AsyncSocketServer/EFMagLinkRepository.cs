﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace New_MagLink
{
    class EFMagLinkRepository: IEFMagLinkRepository
    {
        private MagLink_engineEntities _magDb = new MagLink_engineEntities();



        public IEnumerable<AckMessage> GetAckMessage()
        {
            IEnumerable<AckMessage> ackMessages = new List<AckMessage>();

            try
            {
                return _magDb.AckMessages.ToList();
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error loading The ACk messages");
            }
            return ackMessages;
        }


        public void Dispose()
        {
            _magDb.Dispose();
            
        }

        public Registry GetRegistry()
        {

            Registry registry = new Registry();
            
            try
            {
                return _magDb.Registries.FirstOrDefault(p => p.ID == 1);
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error accessing the registry table");
                
            }

            return registry;
        }

        public void CreateRegistry(Registry registry)
        {
          //  _magDb.Registries.      //Add(registry);
            try
            {
                _magDb.SaveChanges();

            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error saving changes");
            }
            
        }

        public void SaveChanges()
        {
            _magDb.SaveChanges();

        }
        public void CreateMhistory(String message)
        {
            try
            {
                Message_History history = new Message_History();
                history.DateTime = System.DateTime.Now;
                history.Message = message;
                Message m = new Message(message);
                history.PatID = m.getElement("PID", 3);
                history.PatName = m.getElement("PID", 5);
                history.messageid = m.getElement("MSH", 9);
                _magDb.Message_History.Add(history);
                _magDb.SaveChanges();

            }
            catch (Exception ex)
            {
                    ErrorHandler._ErrorHandler.LogError(ex, "Error entering data in the message history table");

            }
            
        }

        public void ProcessQueue(String messageID)
        {
            try
            {
                Queue queue = _magDb.Queues.FirstOrDefault(q => q.MessageID.ToUpper().Trim() == messageID.ToUpper().Trim() && q.Garbage == false);
                if (queue != null)
                {
                    queue.Sent = true;
                    queue.Garbage = true;
                    _magDb.SaveChanges();

                }
            }
            catch (Exception ex)
            {
                    
                ErrorHandler._ErrorHandler.LogError(ex, "Error updating the queue table record after ack");
            }
            

        }

        public IEnumerable<Queue> QueueToSend()
        {
            IEnumerable<Queue> queues = new List<Queue>();
            try
            {
               return _magDb.Queues.Where(q => q.Garbage == false);
            }
            catch (Exception ex)
            {

                ErrorHandler._ErrorHandler.LogError(ex, "Error updating the queue table record after ack");
            }

            return queues;
        }


        public Queue CreateQueueRecord(String message)
        {
            Queue queue = new Queue();

            try
            {
                queue.SentDateTime = System.DateTime.Now;
                queue.Message = message;
                Message m = new Message(message);
                queue.MessageID = m.getElement("MSH", 9);
                queue.Garbage = false;
                queue.Sent = false;
                queue.PatientMRN = m.getElement("PID", 3);
                _magDb.Queues.Add(queue);
                _magDb.SaveChanges();

            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error making queue record in table");                    
            }

            return queue;

        }

    }

   
}