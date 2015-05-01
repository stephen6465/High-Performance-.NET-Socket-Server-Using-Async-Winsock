﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using AsyncSocketServer;
using New_MagLink;
using SQLite.Designer.Design;

namespace New_MagLink
{
    /// <summary>
    /// class OSClient : OSCore
    /// This is a client class that I added into this project
    /// </summary>
    class OSClient : OSCore
    {
        IEFMagLinkRepository _repository;
        public List<Message> MessagesOut = new List<Message>();
        static System.Timers.Timer _timerClient = new System.Timers.Timer();
        static System.Timers.Timer _timerClientConnecTimer = new System.Timers.Timer();
        public static bool connected; 
        SocketAsyncEventArgs item = new SocketAsyncEventArgs();

        public OSClient(IEFMagLinkRepository repository)
        {
            connected = false;
            _repository = repository;
            item.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
            item.SetBuffer(new Byte[Convert.ToInt32(Settings._instance.BufferSize)], 0, Convert.ToInt32(Settings._instance.BufferSize));
            _timerClient.Elapsed += new System.Timers.ElapsedEventHandler(_timerClient_Elapsed);
            _timerClient.Interval = 10000;
            _timerClient.Start();
            _timerClientConnecTimer.Elapsed += new System.Timers.ElapsedEventHandler(_timerClientConnecTimer_Elapsed);
            _timerClientConnecTimer.Interval = 10000;
            _timerClientConnecTimer.Start();

        }

        // This method is used to send a message to the server
        public bool Send(string cmdstring)
        {
            cmdstring = HL7.CreateMLLPMessage(cmdstring);
            exceptionthrown = false;
            //var parameters = os_util.ParseParams(cmdstring);
            if (cmdstring.Length > 0)
            {
                try
                {
                    // We need a connection to the server to send a message
                    if (connected)
                    {

                        byte[] byData = System.Text.Encoding.ASCII.GetBytes(cmdstring);

                        try
                        {
                            connectionsocket.Send(byData);
                        }
                        catch (Exception ex)
                        {
                            ErrorHandler._ErrorHandler.LogError(ex, "Error sending", this);
                            connected = false;
                            Connect(Settings._instance.RemoteIPAddress, Convert.ToInt32(Settings._instance.RemotePort));

                        }

                        bool IOPending = connectionsocket.ReceiveAsync(item);
                        if (!IOPending)
                        {
                            ProcessReceive(item);
                        }
                        return true;
                    }
                    else
                    {
                        connected = false;
                        Connect(Settings._instance.RemoteIPAddress, Convert.ToInt32(Settings._instance.RemotePort));
                        return false;

                    }
                }
                catch (Exception ex)
                {
                    ErrorHandler._ErrorHandler.LogError(ex, "Error sending", this);
                    lasterror = ex.ToString();
                    return false;
                }
            }
            else
            {
                lasterror = "No message provided for Send.";
               ErrorHandler._ErrorHandler.LogInfo(lasterror);
                
                return false;
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs readSocket)
        {
            // if BytesTransferred is 0, then the remote end closed the connection
            if (readSocket.BytesTransferred > 0)
            {
                //SocketError.Success indicates that the last operation on the underlying socket succeeded
                if (readSocket.SocketError == SocketError.Success)
                {
                    OSUserToken token = readSocket.UserToken as OSUserToken;
                    if (token.ReadClientSocketData(readSocket))
                    {
                        Socket readsocket = token.OwnerSocket;

                        // If the read socket is empty, we can do something with the data that we accumulated
                        // from all of the previous read requests on this socket
                        if (readsocket.Available == 0)
                        {
                            token.ProcessClientData(readSocket);
                        }

                        // Start another receive request and immediately check to see if the receive is already complete
                        // Otherwise OnIOCompleted will get called when the receive is complete
                        // We are basically calling this same method recursively until there is no more data
                        // on the read socket
                          ProcessReceive(readSocket);
                        
                    }
                    else
                    {
                        ErrorHandler._ErrorHandler.LogError("Error with read token", this);
                       
                    }

                }
                else
                {
                    ProcessError(readSocket);
                }
            }
            
        }


   

        private void ProcessError(SocketAsyncEventArgs readSocket)
        {
            //Console.WriteLine(readSocket.SocketError.ToString());
            // CloseReadSocket(readSocket);
            ErrorHandler._ErrorHandler.LogInfo(readSocket.SocketError.ToString());
            this.Stop();
            
        }

        private void OnIOCompleted(object sender, SocketAsyncEventArgs e)
        {
            // Determine which type of operation just completed and call the associated handler.
            // We are only processing receives right now on this server.
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    this.ProcessReceive(e);
                    break;
               case SocketAsyncOperation.Send:
               // this.ProcessSend(e);
                break;
                default:
                    ErrorHandler._ErrorHandler.LogInfo("The last operation was invalid not a send or recieve ");    
                    break;
                //throw new ArgumentException("The last operation completed on the socket was not a Receive ");

            }
        }

        public void myFileWatcher_ChangeDetecter(object sender,
        System.IO.FileSystemEventArgs e)
        {
            // do something here.... as in read the file in and check it and then send it to the server 
            //if we are connected. if not connect and then send off
            using (StreamReader newMsg = new StreamReader(e.FullPath))
            {
                String msg = "";
                Queue queue = new Queue();
                try
                {
                    msg = newMsg.ReadToEnd();
                }
                catch (Exception ex)
                {
                        ErrorHandler._ErrorHandler.LogError(ex, "Error opening file to send messages out bound");
                    
                }
               _repository.CreateMhistory(msg);
               queue = _repository.CreateQueueRecord(msg);
               
                try
                {
                    if (Send(msg))
                    {
                        queue.Sent = true;
                        _repository.SaveChanges();

                    };
                }
                catch (Exception ex)
                {
                        ErrorHandler._ErrorHandler.LogError(ex, "Error sending this message: "+msg);
                }
                
            }
        }


        public void SendMessages()
        {

            foreach (var message in Directory.GetFiles(Settings._instance.OutFolderPath,"*.txt"))
            {

                Queue queue = new Queue();
                try
                {
                    using (FileStream f = new FileStream(message, FileMode.Open, FileAccess.ReadWrite))
                    {
                        using (StreamReader newMsg = new StreamReader(f))
                        {

                            String msg = newMsg.ReadToEnd();
                            _repository.CreateMhistory(msg);

                            queue = _repository.CreateQueueRecord(msg);

                            if (Send(msg))
                            {
                               queue.Sent = true;
                                _repository.SaveChanges();

                            }
                            
                        }
                    }

                }
                catch (Exception ex)
                {
                    ErrorHandler._ErrorHandler.LogError( ex, "- Problem sending message - "+ message);
                    
                }
                
            }

        }



        public void fileWatcherStart()
        {

            String[] filters = { "*.txt", "*.hl7" };
            List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
            //     MyWatcher.Path = intSettings.OutFolderPath;

            foreach (string f in filters)
            {
                FileSystemWatcher w = new FileSystemWatcher();
                w.Filter = f;
                w.Path = Settings._instance.OutFolderPath;
                w.IncludeSubdirectories = false;
                // Enable the component to begin watching for changes.
                w.EnableRaisingEvents = true;

                w.Changed += new System.IO.FileSystemEventHandler(this.myFileWatcher_ChangeDetecter);
                w.Created += new System.IO.FileSystemEventHandler(this.myFileWatcher_ChangeDetecter);
                watchers.Add(w);
            }

        }


        // This method disconnects us from the server
        public void DisConnect()
        {
            try
            {
                connectionsocket.Disconnect(true);
                connected = false;
            }
            catch
            {
                //nothing to do since connection is already closed
            }
        }

        public override void Stop()
        {
            connectionsocket.Close();
            connected = false;
           // mutex.ReleaseMutex();
        }

        // This method connects us to the server.
        // Winsock is very optimistic about connecting to the server.
        // It will not tell you, for instance, if the server actually accepted the connection.  It assumes that it did.
        public bool Connect(string iporname, int port)
        {
            exceptionthrown = false;
            
            if (!connected )
            {

                if (CreateSocket(iporname, port))
                {
                    try
                    {
                        var connectendpoint = CreateIPEndPoint(iporname, port);
                        connectionsocket.Connect(connectionendpoint);
                        item.UserToken = new OSUserToken(this.connectionsocket,
                        Convert.ToInt32(Settings._instance.BufferSize), this._repository);
                       
                        ErrorHandler._ErrorHandler.LogInfo("Connected");
                        connected = true;
                        return true;
                    }
                    catch (Exception ex)
                    {

                        ErrorHandler._ErrorHandler.LogError(ex, "There is an error connecting");
                        exceptionthrown = true;
                        //lasterror = ex.ToString();
                        connected = false;
                        return false;
                    }
                }
                else
                {

                    ErrorHandler._ErrorHandler.LogInfo("Can't connect to " + iporname + " on port " + port);
                    connected = false;
                    return false;
                }

            } 

            return true;
        }

        public void _timerClient_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _timerClient.Stop();
            // do stuff for the client timer
            Console.WriteLine("here in the queue timer");
           try
           {
               if (connected)
               {
                   //_timerClientConnecTimer.Stop();
                   SendMessages();
                   CheckQueue();
                   var registry = _repository.GetRegistry();
                   registry.HeartBeat = System.DateTime.Now;
                   registry.Status = "ON";
                   _repository.CreateRegistry(registry);

               }
               else
               {
                   Connect(Settings._instance.RemoteIPAddress, Convert.ToInt32(Settings._instance.RemotePort));
               }
           }
           catch (Exception ex)
           {
                // do something here
               
           }
           _timerClient.Start();
          // _timerClientConnecTimer.Start();
        }

       public void _timerClientConnecTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
           
       }

        private void CheckQueue()
       {
            IEnumerable<Queue> queues = _repository.QueueToSend();
            if (queues.Count() > 0)
            {
                foreach (var queue in queues)
                {
                    try
                    {
                        if (Send(queue.Message))
                        {
                            queue.Sent = true;
                            _repository.SaveChanges();
                            connected = true;
                        }
                    }
                    catch (Exception)
                    {
                         Connect(Settings._instance.RemoteIPAddress,
                            Convert.ToInt32(Settings._instance.RemotePort));
                         connected = false;
                        throw;
                    }

                }

            }

       }
    }
}
