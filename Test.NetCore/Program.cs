using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
                if (!Debugger.IsAttached)
                {
                    if (args.Length == 0)
                    {
                        Console.Error.WriteLine("Usage: {0} FILE [FILE ...]",
                            typeof(Program).Assembly.GetName().Name);
                        return 1;
                    }

                    if (args[0] == "-self")
                    {
                        SelfTest();
                    }
                    else
                    {
                        DumpLockInfo(args);
                    }
                }
                else
                {
                    SelfTest();
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

        private static void DumpLockInfo(string[] paths)
        {
            var infos = LockManager.GetLockingProcessInfos(paths?.ToList(), LockManagerFeatures.UseLowLevelApi);
            if (!infos.Any())
            {
                Console.WriteLine("No locking processes found.");
                return;
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
                Console.WriteLine("Process File Path : {0}", p.ExecutableFullPath);
                Console.WriteLine("Process User Name : {0}", p.Owner);
                Console.WriteLine("Executable  Name  : {0}", p.ExecutableName);
                Console.WriteLine("Application Name  : {0}", p.ApplicationName);
                Console.WriteLine("Session ID        : {0}", p.SessionId);
                first = false;
            }
        }

        public static void SelfTest()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");

            try
            {
                using (var file = File.Open(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    DumpLockInfo(new[] { tempFile });
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
