using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_MagLink
{
    internal class ErrorHandler
    {
        public static ErrorHandler _ErrorHandler = new ErrorHandler();
        public const String LogFileName = "MagLink_Engine_ErrorLog.txt";
        public String path = Directory.GetCurrentDirectory();
        public Exception ex;
        public String message; 
        IEFMagLinkRepository _repository = new EFMagLinkRepository();

        public ErrorHandler()
        {

        }


        public void LogError(String message, OSCore core )
        {
            try
            {
                String LogFilePath = Path.Combine(path, LogFileName);
                using (FileStream f = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write))
                {
                    using (var file = new System.IO.StreamWriter(f))
                    {

                        file.WriteLine(String.Format("Error - {0} - Error @ {1} ", message, System.DateTime.Now));
                    }
                }
            }
            catch (Exception ex1)
            {

                this.LogInfo("Error occured accessing error txt file");
            }
            //this.createLogFile();
            Registry registry; 
           Task<Registry> t  = _repository.GetRegistryAsync();
           
            t.Wait();
            registry = t.Result;

            registry.HeartBeat = System.DateTime.Now;
            registry.ErrorState = "ERROR";
            registry.Status = "OFF";
            registry.ErrorMessage = message;
           Task t3 = _repository.CreateRegistryAsync(registry);
           t3.Wait();
            LogInfo("Shutting down");
           // core.Stop();
        }

        public void LogError(Exception ex, String message)
        {
            //this.createLogFile();
            try
            {
                String LogFilePath = Path.Combine(path, LogFileName);
                using (FileStream f = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write))
                {
                    using (var file = new System.IO.StreamWriter(f))
                    {
                        file.WriteLine(String.Format("{0} - {1} - Error @ {2}", ex.Message, message, System.DateTime.Now));

                    }
                }
            }
            catch (Exception ex1)
            {
                this.LogInfo("Error occured accessing error txt file");
        
                
            }


            Registry registry;
            Task<Registry> t = _repository.GetRegistryAsync();
            t.Wait();
            registry = t.Result;
            
            registry.HeartBeat = System.DateTime.Now;
            registry.ErrorState = "ERROR";
            registry.Status = "OFF";
            registry.ErrorMessage = message;
           Task t2 = _repository.CreateRegistryAsync(registry);
            
            this.LogInfo("Shutting down");
            


        }

        public void LogError(Exception ex , String message, OSCore core )
        {
            //this.createLogFile();
            try
            {
                String LogFilePath = Path.Combine(path, LogFileName);
                using (FileStream f = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write))
                {
                    using (var file = new System.IO.StreamWriter(f))
                    {
                        file.WriteLine(String.Format("{0} - {1} - Error @ {2}", ex.Message, message, System.DateTime.Now));

                    }
                }
            
            }
            catch (Exception ex1)
            {
                this.LogInfo("Error occured accessing error txt file");
    
                
            }
            Registry registry;
            Task<Registry> t = _repository.GetRegistryAsync();
            t.Wait();
            registry = t.Result;
            
            registry.HeartBeat = System.DateTime.Now;
            registry.ErrorState = "ERROR";
            registry.Status = "OFF";
            registry.ErrorMessage = message;
            _repository.CreateRegistryAsync(registry);
            this.LogInfo("Shutting down");
           // core.Stop();


        }

        public void LogInfo(String message )
        {
            //this.createLogFile();
            try
            {
                String LogFilePath = Path.Combine(path, LogFileName);
                using (FileStream f = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write))
                {

                    using (var file = new System.IO.StreamWriter(f))
                    {
                        file.WriteLine(String.Format("Information - {0} - Message @ {1}", message, System.DateTime.Now));

                    }
                }

            }
            catch (Exception ex)
            {
                
                this.LogInfo("Error occured accessing error txt file");

            }
            Registry registry;
            Task<Registry> t = _repository.GetRegistryAsync();
            t.Wait();
            registry = t.Result;
            
            registry.HeartBeat = System.DateTime.Now;
            
            registry.Status = "OFF";
            registry.ErrorMessage = message;
            registry.ErrorState = "ERROR";
            _repository.CreateRegistryAsync(registry);

        }


        public void createLogFile()
        {
            String file = Path.Combine(path, LogFileName);
            if (!File.Exists(file))
            {
                var fs = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite, 4096,FileOptions.None) ;
    
            }

        }

    }


 }

