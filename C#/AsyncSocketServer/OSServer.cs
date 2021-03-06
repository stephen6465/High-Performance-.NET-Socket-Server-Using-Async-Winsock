﻿using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AsyncSocketServer;

namespace New_MagLink
{

    /// <summary>
    /// class OSServer : OSCore
    /// This is the server class that is derived from OSCore.
    /// It creates a server that listens for client connections, then receives
    /// text data from those clients and writes it to the console screen
    /// </summary>
   
    class OSServer : OSCore
    {
        // We limit this server client connections for test purposes
        protected const int DEFAULT_MAX_CONNECTIONS = 1000;
         IEFMagLinkRepository _repository;
        // We use a Mutex to block the listener thread so that limited client connections are active
        // on the server.  If you stop the server, the mutex is released. 
        private static Mutex mutex;

        // Here is where we track the number of client connections
        protected int numconnections;

        // Here is where we track the totalbytes read by the server
        protected int totalbytesread;

        // Here is our stack of available accept sockets
        protected OSAsyncEventStack socketpool;


        // Default constructor
        public OSServer(IEFMagLinkRepository repository)
        {
            _repository = repository;
            exceptionthrown = false;

            // First we set up our mutex and semaphore
            mutex = new Mutex();
            numconnections = 0;

            // Then we create our stack of read sockets
            socketpool = new OSAsyncEventStack(Convert.ToInt32(Settings._instance.NumConnect));

            // Now we create enough read sockets to service the maximum number of clients
            // that we will allow on the server
            // We also assign the event handler for IO Completed to each socket as we create it
            // and set up its buffer to the right size.
            // Then we push it onto our stack to wait for a client connection
            for (Int32 i = 0; i < Convert.ToInt32(Settings._instance.NumConnect); i++)
            {
                SocketAsyncEventArgs item = new SocketAsyncEventArgs();
                item.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                item.SetBuffer(new Byte[Convert.ToInt32(Settings._instance.BufferSize)], 0, Convert.ToInt32(Settings._instance.BufferSize));
                socketpool.Push(item);
            }
            
        }


        // This method is called when there is no more data to read from a connected client
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
                    throw new ArgumentException("The last operation completed on the socket was not a receive");
            }
        }


        // We call this method once to start the server if it is not started
        public bool Start()
        {
            exceptionthrown = false;

            // First create a generic socket
            if (CreateSocket(Settings._instance.LocalIPAddress, Convert.ToInt32(Settings._instance.LocalPort)))
            {
                try
                {
                    // Now make it a listener socket at the IP address and port that we specified
                    connectionsocket.Bind(connectionendpoint);

                    // Now start listening on the listener socket and wait for asynchronous client connections
                    connectionsocket.Listen(Convert.ToInt32(Settings._instance.NumConnect));
                    StartAcceptAsync(null);
                    mutex.WaitOne();
                    return true;
                }
                catch (Exception ex)
                {
                    exceptionthrown = true;
                    lasterror = ex.ToString();
                    return false;
                }
            }
            else
            {
                lasterror = "Unknown Error in Server Start.";
                return false;
            }
        }

        // This method is called once to stop the server if it is started.
        // We could check for the open socket here
        // to stop some exception noise.
        public override void Stop()
        {
            connectionsocket.Close();
            mutex.ReleaseMutex();
        }


        // This method implements the asynchronous loop of events
        // that accepts incoming client connections
        public void StartAcceptAsync(SocketAsyncEventArgs acceptEventArg)
        {
            // If there is not an accept socket, create it
            // If there is, reuse it
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
            }
            else
            {
                acceptEventArg.AcceptSocket = null;
            }

            // this will return true if there is a connection
            // waiting to be processed (IO Pending) 
            bool acceptpending = connectionsocket.AcceptAsync(acceptEventArg);

            // If not, we can go ahead and process the accept.
            // Otherwise, the Completed event we tacked onto the accept socket will do it when it completes
            //if (!acceptpending)
            //{
            //    // Process the accept event
                ProcessAccept(acceptEventArg);
            //}
        }


        // This method is triggered when the accept socket completes an operation async
        // In the case of our accept socket, we are looking for a client connection to complete
        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs AsyncEventArgs)
        {
            ProcessAccept(AsyncEventArgs);
        }


        // This method is used to process the accept socket connection
        private void ProcessAccept(SocketAsyncEventArgs AsyncEventArgs)
        {
            // First we get the accept socket from the passed in arguments
            Socket acceptsocket = AsyncEventArgs.AcceptSocket;

            // If the accept socket is connected to a client we will process it
            // otherwise nothing happens
            if (acceptsocket.Connected)
            {
                try
                {
                    // Go get a read socket out of the read socket stack
                    SocketAsyncEventArgs readsocket = socketpool.Pop();

                    // If we get a socket, use it, otherwise all the sockets in the stack are used up
                    // and we can't accept anymore connections until one frees up
                    if (readsocket != null)
                        
                    {
                        // Create our user object and put the accept socket into it to use later
                        readsocket.UserToken = new OSUserToken(acceptsocket, Convert.ToInt32(Settings._instance.BufferSize), this._repository);

                        // We are not using this right now, but it is useful for counting connections
                        Interlocked.Increment(ref numconnections);

                        // Start a receive request and immediately check to see if the receive is already complete
                        // Otherwise OnIOCompleted will get called when the receive is complete
                        bool IOPending = acceptsocket.ReceiveAsync(readsocket);
                       // comment this for faster reads
                      if (!IOPending)
                        {
                            ProcessReceive(readsocket);
                        }
                    }
                    else
                    {
                        acceptsocket.Close();
                       // Console.WriteLine("Client connection refused because the maximum number of client connections allowed on the server has been reached.");
                        var ex = new Exception("No more connections can be accepted on the server.");
                        ErrorHandler._ErrorHandler.LogError(ex, "Client connection refused because the maximum number of client connections allowed on the server has been reached.");
                        
                        throw ex;
                    }
                }
                catch (Exception ex)
                {
                    exceptionthrown = true;
                    lasterror = ex.ToString();
                }

                // Start the process again to wait for the next connection
                StartAcceptAsync(AsyncEventArgs);
            }
        }


        // This method processes the read socket once it has a transaction
        private void ProcessReceive(SocketAsyncEventArgs readSocket)
        {
            // if BytesTransferred is 0, then the remote end closed the connection
            if (readSocket.BytesTransferred > 0)
            {
                //SocketError.Success indicates that the last operation on the underlying socket succeeded
                if (readSocket.SocketError == SocketError.Success)
                {
                    OSUserToken token = readSocket.UserToken as OSUserToken;
                    if (token.ReadSocketData(readSocket))
                    {
                        Socket readsocket = token.OwnerSocket;

                        // If the read socket is empty, we can do something with the data that we accumulated
                        // from all of the previous read requests on this socket
                        if (readsocket.Available == 0)
                        {
                            token.ProcessData(readSocket);
                        }

                        // Start another receive request and immediately check to see if the receive is already complete
                        // Otherwise OnIOCompleted will get called when the receive is complete
                        // We are basically calling this same method recursively until there is no more data
                        // on the read socket
                        bool IOPending = readsocket.ReceiveAsync(readSocket);
                       if (!IOPending)
                       {
                            ProcessReceive(readSocket);
                        }
                    }
                    else
                    {
                        ErrorHandler._ErrorHandler.LogInfo(token.LastError);
                       // Console.WriteLine(token.LastError);
                        CloseReadSocket(readSocket);
                    }

                }
                else
                {
                    ProcessError(readSocket);
                }
            }
            else
            {
                CloseReadSocket(readSocket);
            }
        }

        
        private void ProcessError(SocketAsyncEventArgs readSocket)
        {
            //Console.WriteLine(readSocket.SocketError.ToString());
            ErrorHandler._ErrorHandler.LogInfo(readSocket.SocketError.ToString());
            CloseReadSocket(readSocket);
        }


        // This overload of the close method doesn't require a token
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
            Interlocked.Decrement(ref numconnections);

            // Put the read socket back in the stack to be used again
            socketpool.Push(readSocket);
        }
    }
}
