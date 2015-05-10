using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
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
        protected const int DEFAULT_MAX_CONNECTIONS = 1000;
        IEFMagLinkRepository _repository;
        public List<Message> MessagesOut = new List<Message>();
        public static System.Timers.Timer _timerClient = new System.Timers.Timer();
        public static  System.Timers.Timer _timerClientConnecTimer = new System.Timers.Timer();
        public static bool connected; 
        //SocketAsyncEventArgs item = new SocketAsyncEventArgs();
        protected int numconnections;
        protected int totalbytesread;
        public OSAsyncEventStack socketpool;
        private static Mutex mutex;
        public static ManualResetEvent connectDone;
        public static ManualResetEvent sendDone;
        public static ManualResetEvent receiveDone;
        public static String response;
        SocketAsyncEventArgs item;

        public OSClient(IEFMagLinkRepository repository)
        {

            // First we set up our mutex and semaphore
            mutex = new Mutex();
            connected = false;
            _repository = repository;
           item = new SocketAsyncEventArgs();
            _timerClient.Elapsed += new System.Timers.ElapsedEventHandler(_timerClient_Elapsed);
            _timerClient.Interval = 10000;
            _timerClient.Start();
            _timerClientConnecTimer.Elapsed += new System.Timers.ElapsedEventHandler(_timerClientConnecTimer_Elapsed);
            _timerClientConnecTimer.Interval = 15000;
            _timerClientConnecTimer.Start();
            numconnections = 0;
            item.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
            item.SetBuffer(new Byte[Convert.ToInt32(Settings._instance.BufferSize)], 0, Convert.ToInt32(Settings._instance.BufferSize));
            socketpool = new OSAsyncEventStack(Convert.ToInt32(Settings._instance.NumConnect));
           
            for (Int32 i = 0; i < Convert.ToInt32(Settings._instance.NumConnect); i++)
            {
                SocketAsyncEventArgs item1 = new SocketAsyncEventArgs();
                item1.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                item1.SetBuffer(new Byte[Convert.ToInt32(Settings._instance.BufferSize)], 0, Convert.ToInt32(Settings._instance.BufferSize));
                socketpool.Push(item1);
            }

        }

        // This method is used to send a message to the server
        public  bool Send(string cmdstring, Queue queue)
        {
            cmdstring = HL7.CreateMLLPMessage(cmdstring);
            exceptionthrown = false;
            //var parameters = os_util.ParseParams(cmdstring);

            Message m = new Message(cmdstring);
            if (m.getElement("MSH", 8).ToUpper().ToString() == "ACK")
                return true;

            if (cmdstring.Length > 0)
            {
                try
                {
                    // We need a connection to the server to send a message
                    if (connectionsocket.Connected)
                    {

                        byte[] byData = System.Text.Encoding.ASCII.GetBytes(cmdstring);

                        try
                        {
                            connectionsocket.Send(byData);
                            queue.Sent = true;
                            _repository.SaveChangesQueue(queue);
                        }
                        catch (Exception ex)
                        {
                            ErrorHandler._ErrorHandler.LogError(ex, "Error sending", this);
                            connected = connectionsocket.Connected ? true : false ;
                            Connect(Settings._instance.RemoteIPAddress, Convert.ToInt32(Settings._instance.RemotePort));

                        }
                       
                        try
                        {

                           //ProcessSyncRecieve();
                           var item1 = socketpool.Pop();
                            item1.UserToken = new OSUserToken(this.connectionsocket,
                        Convert.ToInt32(Settings._instance.BufferSize), this._repository);

                           bool IOPending = connectionsocket.ReceiveAsync(item1);
                           // comment this for faster reads
                           if (!IOPending)
                           {
                               ProcessReceive(item);
                           }


                        }
                        catch (Exception ex)
                        {
                                
                              ErrorHandler._ErrorHandler.LogError(ex, "Did not recieve a message after 3 seconds");  
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


        public Task  ProcessReceiveAsync()
        {
            byte[] bytes = new byte[1024];
            connectionsocket.ReceiveTimeout = 3000;
            int bytesRec = connectionsocket.Receive(bytes);
            String ackMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            return Task.Run(() => ProcessClientDataAsync(ackMessage)) ;
        }

        public void ProcessSyncRecieve()
        {
            byte[] bytes = new byte[1024];
            connectionsocket.ReceiveTimeout = 3000;
            int bytesRec = connectionsocket.Receive(bytes);
            String ackMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

           ProcessClientData(ackMessage);


        }


        public Task ProcessClientDataAsync(String ackMessage)
        {
            return Task.Run(()=> ProcessClientData(ackMessage));

        }

        public void ProcessClientData(String ackMessage)
        {


            // build the end of message string below to check recieved against it
            string content2 = new String((char) HL7.MLLP_FIRST_END_CHARACTER, 1);
            content2 = content2 + new String((char) HL7.MLLP_LAST_END_CHARACTER, 1);

            // get the message up to the eof characters
            // and remove the message from the string builder
            if (ackMessage.IndexOf(content2) > -1)
            {
                if (ackMessage.IndexOf(content2) == 0)
                {

                    ErrorHandler._ErrorHandler.LogInfo(
                        "Should not be here means message recieved did not have start and end MLLP characters ");
                }
                else
                {
                    int temp2 = ackMessage.IndexOf(content2);
                    ackMessage = ackMessage.Substring(1, temp2);
                    _repository.CreateMhistory(ackMessage);
                    Message m = new Message(ackMessage);
                    String messageID = m.getElement("MSH", 9);
                    _repository.CreateAckRecord(ackMessage);
                    _repository.ProcessQueue(messageID);

                }

            }
        }



        public void ProcessReceive(SocketAsyncEventArgs readSocket)
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
                        Socket readsocketRS = token.OwnerSocket;

                        // If the read socket is empty, we can do something with the data that we accumulated
                        // from all of the previous read requests on this socket
                        if (readsocketRS.Available == 0)
                        {
                            token.ProcessClientData(readSocket);
                        }

                        // Start another receive request and immediately check to see if the receive is already complete
                        // Otherwise OnIOCompleted will get called when the receive is complete
                        // We are basically calling this same method recursively until there is no more data
                        // on the read socket

                        bool IOPending = readsocketRS.ReceiveAsync(readSocket);
                        if (!IOPending)
                        {
                            ProcessReceive(readSocket);
                        }
                        
                    }
                    else
                    {
                        ErrorHandler._ErrorHandler.LogError("Error with read token", this);
                       // CloseReadSocket(readSocket);
                    }

                }
                else
                {
                    ProcessError(readSocket);
                }
            }
            else
            {
                //CloseReadSocket(readSocket);
            }
        }

        private void CloseReadSocket(SocketAsyncEventArgs readSocket)
        {
            OSUserToken token = readSocket.UserToken as OSUserToken;
            CloseReadSocket(token, readSocket);
        }


        // This method closes the read socket and gets rid of our user token associated with it
        private void CloseReadSocket(OSUserToken token, SocketAsyncEventArgs readSocket)
        {
            token.Dispose();

            // Decrement the counter keeping track of the total number of clients connected to the server.
           // Interlocked.Decrement(ref numconnections);

            // Put the read socket back in the stack to be used again
          //  socketpool.Push(readSocket);
        }
   

        private void ProcessError(SocketAsyncEventArgs readSocket)
        {
            //Console.WriteLine(readSocket.SocketError.ToString());
             CloseReadSocket(readSocket);
            ErrorHandler._ErrorHandler.LogInfo(readSocket.SocketError.ToString());
            //this.Stop();
            
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
                default:
                    ErrorHandler._ErrorHandler.LogInfo("The last operation was invalid not a send or recieve ");    
                    break;
                //throw new ArgumentException("The last operation completed on the socket was not a Receive ");

            }
        }

        public void myFileWatcher_ChangeDetecter(object sender,
        System.IO.FileSystemEventArgs e)
        {
            _timerClient.Stop();
            Queue queue = new Queue();
            // do something here.... as in read the file in and check it and then send it to the server 
            //if we are connected. if not connect and then send off
            try
            {
                using (StreamReader newMsg = new StreamReader(e.FullPath))
                {
                    String msg = "";

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
                        if (Send(msg, queue))
                        {
                            //queue.Sent = true;
                            // _repository.SaveChangesQueue(queue);

                        };
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler._ErrorHandler.LogError(ex, "Error sending this message: " + msg);
                    }


                }

                if (queue.Sent == true)
                    File.Delete(e.FullPath);

            }
            catch (Exception ex)
            {
                ErrorHandler._ErrorHandler.LogError(ex, "ERROR in the file watcher");
                _timerClient.Start();
            }
            _timerClient.Start();
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
                            String msg = "";

                             msg = newMsg.ReadToEnd();
                            Message msgMessage = new Message(msg);

                            if (msgMessage.getElement("MSH", 8) == "ACK")
                            {
                                queue.Sent = true;
                                queue.Garbage = true;
                               // _repository.SaveChangesQueue(queue);
                               

                            }
                            else
                            {

                              //  _repository.CreateMhistory(msg);

                               // queue = _repository.CreateQueueRecord(msg);

                                if (Send(msg, queue))
                                {
                                    //queue = _repository.GetQueue(queue.ID);

                                    //queue.Sent = true;
                                    //_repository.SaveChangesQueue(queue);

                                }
                            }
                        }

                      
                    }
                    if (File.Exists(message))
                    {
                        if (queue.Sent == true)
                            File.Delete(message);
                    }
                }
                catch (Exception ex)
                {
                    ErrorHandler._ErrorHandler.LogError( ex, "- Problem sending message - "+ message);
                    
                }
                
            }

        }

        public void StarStopTimer(String command)
        {
            if (command.ToUpper().Trim() == "START")
            {
                _timerClient.Start();
            }
            if (command.ToUpper().Trim() == "STOP")
            {
                _timerClient.Stop();
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

               // w.Changed += new System.IO.FileSystemEventHandler(this.myFileWatcher_ChangeDetecter);
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
            mutex.ReleaseMutex();
        }

        // This method connects us to the server.
        // Winsock is very optimistic about connecting to the server.
        // It will not tell you, for instance, if the server actually accepted the connection.  It assumes that it did.
        public bool Connect(string iporname, int port)
        {
            exceptionthrown = false;
            if (connectionsocket == null)
            {
                if (CreateSocket(iporname, port))
                {
                    try
                    {
                        var connectendpoint = CreateIPEndPoint(iporname, port);
                        connectionsocket.Connect(connectionendpoint);

                        item.UserToken = new OSUserToken(this.connectionsocket,
                            Convert.ToInt32(Settings._instance.BufferSize), this._repository);

                        bool IOPending = connectionsocket.ReceiveAsync(item);
                        // comment this for faster reads
                        if (!IOPending)
                        {
                            ProcessReceive(item);
                        }


                        return true;

                    }
                    catch (Exception ex)
                    {
                        ErrorHandler._ErrorHandler.LogInfo("Can't connect to " + iporname + " on port " + port);
                        connected = false;
                        return false;

                    }

                }
            }

           

            if (!connectionsocket.Connected)
                {

                    if (CreateSocket(iporname, port))
                    {
                        try
                        {
                            var connectendpoint = CreateIPEndPoint(iporname, port);
                            connectionsocket.Connect(connectionendpoint);

                            // Go get a read socket out of the read socket stack
                            
                            item.UserToken = new OSUserToken(this.connectionsocket,
                             Convert.ToInt32(Settings._instance.BufferSize), this._repository);
                            
                            // //readsocket.UserToken = new OSUserToken(this.connectionsocket,
                            // Convert.ToInt32(Settings._instance.BufferSize), this._repository);
                            //SocketAsyncEventArgs readsocket = socketpool.Pop();
                            //readsocket.UserToken = new OSUserToken(this.connectionsocket,
                            // Convert.ToInt32(Settings._instance.BufferSize), this._repository);
                            //ErrorHandler._ErrorHandler.LogInfo("Connected");
                            //connected = true;

                            bool IOPending = connectionsocket.ReceiveAsync(item);
                            // comment this for faster reads
                            if (!IOPending)
                            {
                                ProcessReceive(item);
                            }

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
        public enum SocketErrorCodes
        {
            InterruptedFunctionCall = 10004,
            PermissionDenied = 10013,
            BadAddress = 10014,
            InvalidArgument = 10022,
            TooManyOpenFiles = 10024,
            ResourceTemporarilyUnavailable = 10035,
            OperationNowInProgress = 10036,
            OperationAlreadyInProgress = 10037,
            SocketOperationOnNonSocket = 10038,
            DestinationAddressRequired = 10039,
            MessgeTooLong = 10040,
            WrongProtocolType = 10041,
            BadProtocolOption = 10042,
            ProtocolNotSupported = 10043,
            SocketTypeNotSupported = 10044,
            OperationNotSupported = 10045,
            ProtocolFamilyNotSupported = 10046,
            AddressFamilyNotSupported = 10047,
            AddressInUse = 10048,
            AddressNotAvailable = 10049,
            NetworkIsDown = 10050,
            NetworkIsUnreachable = 10051,
            NetworkReset = 10052,
            ConnectionAborted = 10053,
            ConnectionResetByPeer = 10054,
            NoBufferSpaceAvailable = 10055,
            AlreadyConnected = 10056,
            NotConnected = 10057,
            CannotSendAfterShutdown = 10058,
            ConnectionTimedOut = 10060,
            ConnectionRefused = 10061,
            HostIsDown = 10064,
            HostUnreachable = 10065,
            TooManyProcesses = 10067,
            NetworkSubsystemIsUnavailable = 10091,
            UnsupportedVersion = 10092,
            NotInitialized = 10093,
            ShutdownInProgress = 10101,
            ClassTypeNotFound = 10109,
            HostNotFound = 11001,
            HostNotFoundTryAgain = 11002,
            NonRecoverableError = 11003,
            NoDataOfRequestedType = 11004
        }

        public void _timerClient_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _timerClient.Stop();
            // do stuff for the client timer
            Console.WriteLine("here in the queue timer");
           try
           {
               ClearOldQueue();
               if (connectionsocket.Connected)
               {
                   //_timerClientConnecTimer.Stop();
                   SendMessages();
                  // CheckQueue();
                   
                   var registry = _repository.GetRegistry();
                   registry.HeartBeat = System.DateTime.Now;
                   registry.Status = "ON";
                   registry.ErrorState = "";
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
               Connect(Settings._instance.RemoteIPAddress, Convert.ToInt32(Settings._instance.RemotePort));
               _timerClient.Start();
           }
           _timerClient.Start();
          // _timerClientConnecTimer.Start();
        }

        private void ClearOldQueue()
        {
            _repository.ClearQueue();
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

                        if (Send(queue.Message, queue))
                        {
                            //queue.Sent = true;
                            //_repository.SaveChangesQueue(queue);
                            connected = true;
                        }
                    }
                    catch (Exception)
                    {
                         Connect(Settings._instance.RemoteIPAddress,
                            Convert.ToInt32(Settings._instance.RemotePort));

                        connected = connectionsocket.Connected  ? true : false;
                        throw;
                    }

                }

            }

       }
    }
}
   ////Creates the Socket for sending data over TCP.
   // Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
   //    ProtocolType.Tcp );

   // // Connects to host using IPEndPoint.
   // s.Connect(EPhost);
   // if (!s.Connected)
   // {
   //    strRetPage = "Unable to connect to host";
   // }
   // // Use the SelectWrite enumeration to obtain Socket status. 
   //  if(s.Poll(-1, SelectMode.SelectWrite)){
   //       Console.WriteLine("This Socket is writable.");
   //  }
   //  else if (s.Poll(-1, SelectMode.SelectRead)){
   //        Console.WriteLine("This Socket is readable." );
   //  }
   //  else if (s.Poll(-1, SelectMode.SelectError)){
   //       Console.WriteLine("This Socket has an error.");
   //  }