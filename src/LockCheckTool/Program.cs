using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LockCheck
{
    internal class Program
    {
        private static readonly string s_name = typeof(Program).Assembly.GetName().Name;

        private static int Usage()
        {
            Console.Error.WriteLine(@"
Usage:
  {0} [options] <PATH>...

Arguments:
  <PATH>   The path or paths to check for.

Options:
  -d, --include-cwd    Check if a <PATH> is the current working directory for a process.
      --use-rm         Use RestartManager API (Windows only).
", s_name);

            return -1;
        }

        private static int Main(string[] args)
        {
            try
            {
                var features = LockManagerFeatures.UseLowLevelApi;

                int i;
                for (i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--include-cwd" || args[i] == "-d")
                    {
                        features |= LockManagerFeatures.CheckDirectories;
                    }
                    else if (args[i].Equals("--use-rm"))
                    {
                        features &= ~LockManagerFeatures.UseLowLevelApi;
                    }
                    else if (args[i] == "--help" || args[i] == "-h" || args[i] == "-?")
                    {
                        return Usage();
                    }
                    else if (args[i].StartsWith("--") && args[i].Length > 2)
                    {
                        Console.Error.WriteLine($"Unknown option '{args[0]}'. Run `{s_name} --help` for more information.");
                        return -1;
                    }
                    else
                    {
                        // Not an option or only "--".
                        break;
                    }
                }

                args = args.Skip(i).ToArray();
                if (args.Length == 0)
                {
                    return Usage();
                }

                var infos = LockManager.GetLockingProcessInfos(args, features);
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
                    Console.WriteLine("Owner             : {0}", p.Owner);
                    Console.WriteLine("SessionId         : {0}", p.SessionId);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Console.WriteLine("LockAccess        : {0}", p.LockAccess);
                        Console.WriteLine("LockMode          : {0}", p.LockMode);
                        Console.WriteLine("LockType          : {0}", p.LockType);
                    }

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
