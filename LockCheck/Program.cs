using System;
using System.ComponentModel;
using System.Linq;

namespace LockCheck
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.Error.WriteLine("Usage: {0} FILE [FILE ...]",
                        typeof (Program).Assembly.GetName().Name);
                }

                var infos = RestartManager.GetLockingProcessInfos(args);
                if (!infos.Any())
                {
                    Console.WriteLine("No locking processes found.");
                    return 0;
                }

                bool first = true;
                foreach (var p in infos)
                {
                    if (!first)
                    {
                        Console.WriteLine("----------------------------------------------------");
                    }

                    Console.WriteLine("Process ID        : {0}", p.ProcessId);
                    Console.WriteLine("Process Start Time: {0}", p.StartTime.ToString("F"));
                    Console.WriteLine("Process File Path : {0}", p.FilePath);
                    Console.WriteLine("Application Type  : {0}", p.ApplicationType);
                    Console.WriteLine("Application Status: {0}", p.ApplicationStatus);
                    Console.WriteLine("Application Name  : {0}", p.ApplicationName);
                    if (p.ApplicationType == ApplicationType.Service)
                    {
                        Console.WriteLine("Service Name      : {0}", p.ServiceShortName);
                    }
                    Console.WriteLine("TS Session ID     : {0}", p.TerminalServicesSessionId);
                    first = false;
                }
            }
            catch (Win32Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return ex.HResult;
            }
            return 0;
        }
    }
}
