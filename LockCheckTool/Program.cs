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
                        typeof(Program).Assembly.GetName().Name);
                }

                var infos = LockManager.GetLockingProcessInfos(args);
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
                    Console.WriteLine("Application Name  : {0}", p.ApplicationName);
                    Console.WriteLine("Path              : {0}", p.ExecutableFullPath);
                    Console.WriteLine("Process Start Time: {0}", p.StartTime.ToString("F"));
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
