using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
//using System.Byte[];
using System.Threading.Tasks;
using AsyncSocketServer;


namespace New_MagLink
{

    /// <summary>
    /// class OSUserToken : IDisposable
    /// This class represents the instantiated read socket on the server side.
    /// It is instantiated when a server listener socket accepts a connection.
    /// </summary>
  
    sealed class OSUserToken : IDisposable
    {
        // This is a ref copy of the socket that owns this token
        private Socket ownersocket;

        // this stringbuilder is used to accumulate data off of the readsocket
        private StringBuilder stringbuilder;
        public IEFMagLinkRepository _repository;
        // This stores the total bytes accumulated so far in the stringbuilder
        private Int32 totalbytecount;

        // We are holding an exception string in here, but not doing anything with it right now.
        public String LastError;

        // The read socket that creates this object sends a copy of its "parent" accept socket in as a reference
        // We also take in a max buffer size for the data to be read off of the read socket
        public OSUserToken(Socket readSocket, Int32 bufferSize, IEFMagLinkRepository repository)
        {
            _repository = repository;
            ownersocket = readSocket;
            stringbuilder = new StringBuilder(bufferSize);

        }

        // This allows us to refer to the socket that created this token's read socket
        public Socket OwnerSocket
        {
            get
            {
                return ownersocket;
            }
        }


        // Do something with the received data, then reset the token for use by another connection.
        // This is called when all of the data has been received for a read socket.
        public void ProcessData(SocketAsyncEventArgs args)
        {
            // Get the last message received from the client, which has been stored in the stringbuilder.
            String received = stringbuilder.ToString();

            //TODO Use message received to perform a specific operation.

            string content2 = new String((char)HL7.MLLP_FIRST_END_CHARACTER, 1);
            content2 = content2 + new String((char)HL7.MLLP_LAST_END_CHARACTER, 1);

           // Settings settings = new Settings();
            
            // get the message up to the eof characters
            // and remove the message from the string builder
            if (received.IndexOf(content2) > -1)
            {
                if (received.IndexOf(content2) == 0)
                {

                    totalbytecount = 0;
                    stringbuilder.Length = 0;
                   // Console.WriteLine("HERE CLEARING THINGS OUT");
                }
                else
                {

                    //int temp = received.IndexOf(content2);
                    int temp2 = received.IndexOf(content2);
                    totalbytecount = totalbytecount - received.Length;
                    received = received.Substring(1, temp2); 

                    stringbuilder.Remove(0, temp2);
                    totalbytecount = 0;
                    stringbuilder.Length = 0;
                   // Console.WriteLine("Received: \"{0}\". The server has read {1} bytes. {2} buffer {3}", received,received.Length, temp2, stringbuilder.Capacity);
                    _repository.CreateMhistory(received);

                    Message m = new Message(received);
                    //TODO: Load up a send buffer to send an ack back to the calling client
                    //TODO: Change the ack type based on errors or not.
                    AckMessages ack = new AckMessages(received, _repository);
                    
                    

                    if (!string.IsNullOrWhiteSpace(Settings._instance.OutFolderPath))
                    {
                        if (!string.IsNullOrEmpty(ack.getMessageId()))
                        {
                            String path = Path.Combine(Settings._instance.OutFolderPath, ack.getMessageId());

                            if (File.Exists(path))
                            {
                                bool b = true;
                                int i = 0;
                                do
                                {
                                    i++;
                                    path = Path.Combine(Settings._instance.OutFolderPath,
                                        string.Format(ack.getMessageId() + "-{0}{1}", i, ".txt"));
                                    if (!File.Exists(path))
                                    {
                                        b = false;
                                    }

                                } while (b);

                            }

                            using (
                                var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                                    4096,
                                    FileOptions.None))
                            {
                                fs.Write(Encoding.ASCII.GetBytes(received), 0, Encoding.ASCII.GetByteCount(received));
                            }
                           
                            Byte[] sendBuffer = Encoding.ASCII.GetBytes(ack.ack);
                            args.SetBuffer(sendBuffer, 0, sendBuffer.Length);
                            OwnerSocket.Send(args.Buffer);
                            _repository.CreateMhistory(ack.ack);
                            
                        }
                    }
                    else
                    {
                        _repository.CreateQueueRecord(received);
                        ErrorHandler._ErrorHandler.LogInfo("There is an error creating the file: "+ received);                       

                    }
                }
              }
        }

        public void  ProcessClientData(SocketAsyncEventArgs args)
        {
            // Get the last message received from the client, which has been stored in the stringbuilder.
            String received = stringbuilder.ToString();

            //TODO Use message received to perform a specific operation.

            // build the end of message string below to check recieved against it
            string content2 = new String((char)HL7.MLLP_FIRST_END_CHARACTER, 1);
            content2 = content2 + new String((char)HL7.MLLP_LAST_END_CHARACTER, 1);

            // get the message up to the eof characters
            // and remove the message from the string builder
            if (received.IndexOf(content2) > -1)
            {
                if (received.IndexOf(content2) == 0)
                {

                    totalbytecount = 0;
                    stringbuilder.Length = 0;
                    //Console.WriteLine("HERE CLEARING THINGS OUT");
                 // ErrorHandler._ErrorHandler.LogInfo("Cleaning out buffer client data ");
                }
                else
                {
                    //int temp = received.IndexOf(content2);
                    int temp2 = received.IndexOf(content2);
                    totalbytecount = totalbytecount - received.Length;
                    received = received.Substring(1, temp2);
                   //  _repository.CreateMhistory(received);
                    
                    stringbuilder.Remove(0, temp2);
                    totalbytecount = 0;
                    stringbuilder.Length = 0;
                    
                   // Console.WriteLine("Received: \"{0}\". The server has read {1} bytes. {2}", received, received.Length, temp2);

                    Message m = new Message(received);
                    String messageID = m.getElement("MSH", 9);
                    
                     //   _repository.CreateAckRecord(received);
                   // _repository.ProcessQueue(messageID);

                   // AckMessages ack = new AckMessages(received, _repository);
                    //Console.WriteLine(ack.ack);
                    //Byte[] sendBuffer = Encoding.ASCII.GetBytes(ack.ack);
                    //args.SetBuffer(sendBuffer, 0, sendBuffer.Length);
                    //OwnerSocket.Send(args.Buffer);

                }
                
            }

            // All the data has been read from the 
            // client. Display it on the console.
            //Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
            //    content.Length, content);
            // Echo the ACk message here async or in the caller
            //Send(handler, content);

            //TODO: Load up a send buffer to send an ack back to the calling client

            //

            // Clear StringBuffer, so it can receive more data from the client.


        }



        // This method gets the data out of the read socket and adds it to the accumulator string builder
        public bool ReadSocketData(SocketAsyncEventArgs readSocket)
        {
            Int32 bytecount = readSocket.BytesTransferred;

            stringbuilder.Append(Encoding.ASCII.GetString(readSocket.Buffer, readSocket.Offset, bytecount));
            totalbytecount += bytecount;

            //// put my custom Hl7 code here...
            string content;
            string content2 = new String((char)HL7.MLLP_FIRST_END_CHARACTER, 1);
            content2 = content2 + new String((char)HL7.MLLP_LAST_END_CHARACTER, 1);

            content = stringbuilder.ToString();
            if (content.IndexOf(content2) > -1)
            {

                this.ProcessData(readSocket);

            }

            return true;

        }


        public bool ReadClientSocketData(SocketAsyncEventArgs readSocket)
        {
            Int32 bytecount = readSocket.BytesTransferred;

            stringbuilder.Append(Encoding.ASCII.GetString(readSocket.Buffer, readSocket.Offset, bytecount));
            totalbytecount += bytecount;

            //// put my custom Hl7 code here...
            string content;
            string content2 = new String((char)HL7.MLLP_FIRST_END_CHARACTER, 1);
            content2 = content2 + new String((char)HL7.MLLP_LAST_END_CHARACTER, 1);

            content = stringbuilder.ToString();
            if (content.IndexOf(content2) > -1)
            {

                 this.ProcessClientData(readSocket);
               //task.Wait();

            }

            return true;

        }



        // This is a standard IDisposable method
        // In this case, disposing of this token closes the accept socket
        public void Dispose()
        {
            try
            {
                ownersocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                //Nothing to do here, connection is closed already
            }
            finally
            {
                ownersocket.Close();
            }
        }
    }
}
