﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net.Sockets;

namespace New_MagLink
{
    class EFMagLinkRepository: IEFMagLinkRepository
    {
       // private MagLink_engineEntities _magDb = new MagLink_engineEntities();



        public IEnumerable<AckMessage> GetAckMessage()
        {
            IEnumerable<AckMessage> ackMessages = new List<AckMessage>();

            try
            {
                //using (_magDb)
                //{
                var    _magDb = new MagLink_engineEntities();
                    return _magDb.AckMessages.ToList();   
                //}
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error loading The ACk messages");
            }
            return ackMessages;
        }


        public void Dispose()
        {

            this.Dispose();

        }

        public Registry GetRegistry()
        {

            Registry registry = new Registry();
            
            try
            {
                //using (_magDb)
                //{
                  var   _magDb = new MagLink_engineEntities();
                    return _magDb.Registries.FirstOrDefault(p => p.ID == 1); 
                    
                //}
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
                //using (_magDb)
                //{
                 var   _magDb = new MagLink_engineEntities();
                    _magDb.Entry(registry).State = registry.ID == 0 ? EntityState.Added : EntityState.Modified;
                    _magDb.SaveChanges();
                   // _magDb.Dispose();
                //}
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error saving changes to registry");
            }
            
        }

        public void CreateAckRecord(String m)
        {
            //  _magDb.Registries.      //Add(registry);
            try
            {
                var message = new Message(m);

                AckMessRecieved ackMess = new AckMessRecieved();
                ackMess.Message = m;
                ackMess.MessageID = message.getElement("MSH", 9);
                ackMess.SentDateTime = System.DateTime.Now;
                var _magDb = new MagLink_engineEntities();
                _magDb.Entry(ackMess).State = ackMess.ID == 0 ? EntityState.Added : EntityState.Modified;
                _magDb.SaveChanges();
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error saving changes to ackMessages Received");
            }

        }

        public void SaveChangesQueue(Queue queue)
        {
            try
            {
                //using (_magDb)
                //{
                  var   _magDb = new MagLink_engineEntities();
                    //_magDb.Queues.Attach(queue);
                    _magDb.Entry(queue).State = queue.ID == 0 ? EntityState.Added : EntityState.Modified;
                    _magDb.SaveChanges();
                   // _magDb.Dispose();
                //}
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error saving changes to queue call");
            }
        }

        public void SaveChangesMhistory(Message_History mhistory)
    {
           try
            {
                //using (_magDb)
                //{
                  var  _magDb = new MagLink_engineEntities();
                    //_magDb.Message_History.Attach(mhistory);
                _magDb.Entry(mhistory).State = mhistory.mhistID == 0 ? EntityState.Added : EntityState.Modified;
                    _magDb.SaveChanges();
                    //_magDb.Dispose();
                //}
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error saving changes to queue call");
            }
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

                //using (_magDb)
                //{
              var      _magDb = new MagLink_engineEntities();
                    _magDb.Message_History.Add(history);
                    _magDb.SaveChanges();
                  //  _magDb.Dispose();
                //}
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
                var _magDb = new MagLink_engineEntities();
                Queue queue = _magDb.Queues.FirstOrDefault(q => q.MessageID.ToUpper().Trim() == messageID.ToUpper().Trim() && (q.Garbage == false || q.Garbage == null));
                if (queue != null)
                {
                    //using (_magDb)
                    //{
                    
                     
                        queue.Sent = true;
                        queue.Garbage = true;
                        _magDb.Entry(queue).State = queue.ID == 0 ? EntityState.Added : EntityState.Modified;
                    try
                    {
                        _magDb.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler._ErrorHandler.LogError(ex, "Error saving data in the queue table");
                        
                    }

                }

                //AckMessRecieved ackMess =
                //          _magDb.AckMessRecieveds.FirstOrDefault(
                //              a => a.MessageID.Trim().ToUpper() == messageID.Trim().ToUpper());

                //if (ackMess != null)
                //{


                //}
            
            }
            catch (Exception ex)
            {
                    
                ErrorHandler._ErrorHandler.LogError(ex, "Error updating the queue table record after ack");
            }
            

        }

        public Queue GetQueue(Int64 ID)
        {

            var _magDb = new MagLink_engineEntities();
            Queue queue = _magDb.Queues.FirstOrDefault(q => q.ID == ID);

            return queue;
        }

        public IEnumerable<Queue> QueueToSend()
        {
            IEnumerable<Queue> queues = new List<Queue>();
            try
            {
               

                //using (_magDb)
                //{
                    var _magDb = new MagLink_engineEntities();
                    return _magDb.Queues.Where(q => q.Garbage == false).ToList();     
                //}

            }
            catch (Exception ex)
            {

                ErrorHandler._ErrorHandler.LogError(ex, "Error updating the queue table record after ack");
            }

            return queues;
        }

        public void ClearQueue()
        {

            var _magDb = new MagLink_engineEntities();
            var queueRecords = _magDb.Queues.Where(q => q.Garbage == true).ToList();
            _magDb.Queues.RemoveRange(queueRecords);
            _magDb.SaveChanges();


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

                //using (_magDb)
                //{
                   var _magDb = new MagLink_engineEntities();
                    _magDb.Queues.Add(queue);
                    _magDb.SaveChanges();
   
                //}
            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "Error making queue record in table");                    
            }

            return queue;

        }

    }

   
}
