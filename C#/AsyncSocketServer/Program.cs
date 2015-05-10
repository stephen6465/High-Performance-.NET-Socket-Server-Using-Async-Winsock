using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net.Configuration;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using AsyncSocketServer;

namespace New_MagLink
{
     /// <summary>
    /// This is a console app to test the client and server.
    /// It does minimal error handling.
    /// To see the valid commands, start the app and type "help" at the command prompt
    /// </summary>
    class Program
    {
        // We use util, and one server, and one client in this app
        static OSUtil os_util;
        static OSServer os_server;
        static OSClient os_client;
        static System.Timers.Timer _timerHeartBeat = new System.Timers.Timer();
        
        static System.Timers.Timer _timer3 = new System.Timers.Timer();  // Rename this timer when It has a function.
        static EFMagLinkRepository _repository = new EFMagLinkRepository();
        static void Main(string[] args)
        {
            //application state trackers
            bool shutdown = false;
            bool serverstarted = false;
            bool clientstarted = false;

            os_util = new OSUtil();

            //Intialize your settings and settings file
            //Settings. intSettings = new Settings();

            String path = Directory.GetCurrentDirectory();
            String setFile = @"\settings.xml";
            String ComPath = path + setFile;
           
            var registry = new Registry();
            registry = _repository.GetRegistry();

            registry.Status = "ON";
            registry.ErrorMessage = "";
            registry.ErrorState = "";
            registry.HeartBeat = System.DateTime.Now;
            _repository.CreateRegistry(registry);
           
            // set timer to mantain the heartBeat now

            _timerHeartBeat.Elapsed += new System.Timers.ElapsedEventHandler(_timer_Elapsed);
            _timerHeartBeat.Interval = 15000;
            _timerHeartBeat.Start();
            

            //Check for config file and if it doesn't exist then create it

            if (File.Exists(ComPath))
            {
                Settings intSet = Settings._instance.Deserialize(ComPath);
                Settings._instance = intSet;
            }
            else
            {
                //Might want to end here or do something with no config
                Settings._instance.Serialize(ComPath, Settings._instance);
                // to shut down just call os_server.Stop();
            }
            if (Settings._instance.Type.ToUpper().Trim() == "SERVER")
            {
                os_server = new OSServer(_repository);

                bool started = os_server.Start();
                if (!started)
                {
                    ErrorHandler._ErrorHandler.LogInfo(os_server.GetLastError()+ "-Failed to Start Server.");
                    
                }
                else
                {
                    ErrorHandler._ErrorHandler.LogInfo(string.Format("Server started successfully.\nRunning on Port:{0} and IP:{1}", Settings._instance.LocalPort, Settings._instance.LocalIPAddress));
                    
                    serverstarted = true;
                }
            }

            if (Settings._instance.Type.ToUpper().Trim() == "CLIENT")
            {
               var os_client = new OSClient(_repository);
                os_client.fileWatcherStart();

               bool connected = os_client.Connect(Settings._instance.RemoteIPAddress, Convert.ToInt32(Settings._instance.RemotePort));
               
                if (!connected)
               {
                   ErrorHandler._ErrorHandler.LogInfo("Failed to Start Client.");
                   
               }
                else
                {
                    ErrorHandler._ErrorHandler.LogInfo(string.Format("Client started successfully.\nRunning on Port:{0} and IP:{1}", Settings._instance.RemoteIPAddress, Settings._instance.RemotePort));
                    //connected = true;
                    clientstarted = true;
                    os_client.StarStopTimer("Stop");
                   
                  
                    os_client.SendMessages();
                   os_client.StarStopTimer("START");

                 }
            }
            
            
            while (!shutdown)
            {
                string userinput = Console.ReadLine();

                if (!string.IsNullOrEmpty(userinput))
                {
                    switch (os_util.ParseCommand(userinput))
                    {
                        case OSUtil.os_cmd.OS_EXIT:
                            {
                                if (serverstarted)
                                {
                                    registry.Status = "OFF";
                                    registry.HeartBeat = System.DateTime.Now;
                                    _repository.CreateRegistry(registry);
                                    os_server.Stop();
                                }

                                if (clientstarted)
                                {
                                    os_client.DisConnect();
                                    registry.Status = "OFF";
                                    registry.HeartBeat = System.DateTime.Now;
                                    _repository.CreateRegistry(registry);
                                }
                                
                                shutdown = true;
                                break;
                            }
                        
                    }

                }
            }


        }

        static void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _timerHeartBeat.Stop();
            //Console.WriteLine("Timer 1 Hit...");
            var registry = _repository.GetRegistry();
            registry.HeartBeat = System.DateTime.Now;
            registry.Status = "ON";
            _repository.CreateRegistry(registry);
            _timerHeartBeat.Start();
        }

       
    }


     
}
