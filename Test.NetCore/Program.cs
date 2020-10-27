using System;
using System.ComponentModel;
using System.Linq;
using LockCheck;

namespace Test.NetCore
{
    class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.Error.WriteLine("Usage: {0} FILE [FILE ...]",
                        typeof(Program).Assembly.GetName().Name);
                }

                var infos = LockManager.GetLockingProcessInfos(args, LockManagerFeatures.UseLowLevelApi);
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
                    Console.WriteLine("Process User Name : {0}", p.UserName);
                    Console.WriteLine("Executable  Name  : {0}", p.ExecutableName);
                    Console.WriteLine("Application Name  : {0}", p.ApplicationName);
                    Console.WriteLine("Session ID        : {0}", p.SessionId);
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
