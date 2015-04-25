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
            this.createLogFile();
            using (var file = new System.IO.StreamWriter(Path.Combine(path, LogFileName),true))
            {

                file.WriteLine(String.Format("Error - {0} - Error @ {1} ",  message, System.DateTime.Now));
            }
            var registry = _repository.GetRegistry();

            registry.HeartBeat = System.DateTime.Now;
            registry.ErrorState = "ERROR";
            registry.Status = "OFF";
            registry.ErrorMessage = message;
            _repository.CreateRegistry(registry);
            LogInfo("Shutting down");
            core.Stop();
        }

        public void LogError(Exception ex, String message)
        {
            this.createLogFile();
            using (var file = new System.IO.StreamWriter(Path.Combine(path, LogFileName), true))
            {
                file.WriteLine(String.Format("{0} - {1} - Error @ {2}", ex.Message, message, System.DateTime.Now));

            }

            var registry = _repository.GetRegistry();

            registry.HeartBeat = System.DateTime.Now;
            registry.ErrorState = "ERROR";
            registry.Status = "OFF";
            registry.ErrorMessage = message;
            _repository.CreateRegistry(registry);
            this.LogInfo("Shutting down");
            


        }

        public void LogError(Exception ex , String message, OSCore core )
        {
            this.createLogFile();
            using (var file = new System.IO.StreamWriter(Path.Combine(path, LogFileName), true))
            {
                file.WriteLine(String.Format("{0} - {1} - Error @ {2}", ex.Message, message, System.DateTime.Now));

            }
            
            var registry = _repository.GetRegistry();

            registry.HeartBeat = System.DateTime.Now;
            registry.ErrorState = "ERROR";
            registry.Status = "OFF";
            registry.ErrorMessage = message;
            _repository.CreateRegistry(registry);
            this.LogInfo("Shutting down");
            core.Stop();


        }

        public void LogInfo(String message )
        {
            this.createLogFile();
            String LogFilePath = Path.Combine(path, LogFileName);
            using (var file = new System.IO.StreamWriter(LogFilePath, true))
            {
                file.WriteLine(String.Format("Information - {0} - Message @ {1}",  message, System.DateTime.Now));

            }
            var registry = _repository.GetRegistry();
            registry.HeartBeat = System.DateTime.Now;
            
            registry.Status = "OFF";
            registry.ErrorMessage = message;
            registry.ErrorState = "ERROR";
            _repository.CreateRegistry(registry);

        }


        public void createLogFile()
        {
            String file = Path.Combine(path, LogFileName);
            if (!File.Exists(file))
            {
                var fs = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,FileOptions.None) ;
    
            }

        }

    }


 }

