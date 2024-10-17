using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine($"Args: {string.Join(" ", args)}");
        try
        {
            string pipeName = args[0];
            string directory = args[1];
            int sleep = int.Parse(args[2]);

            Environment.CurrentDirectory = directory;
            Console.WriteLine($"Running Current directory is now {Environment.CurrentDirectory}");

            using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
            {
                client.Connect();

                using (var writer = new StreamWriter(client))
                {
                    writer.AutoFlush = true;
#if NET
                    writer.WriteLine($"READY:{Environment.ProcessId}:{Environment.CurrentDirectory}:{IntPtr.Size * 8}");
#else
                    writer.WriteLine($"READY:{System.Diagnostics.Process.GetCurrentProcess().Id}:{Environment.CurrentDirectory}:{IntPtr.Size * 8}");
#endif
                    if (sleep > 0)
                    {
                        Console.WriteLine($"Server signaled, sleeping for {sleep * 1000} seconds ...");
                        Thread.Sleep(sleep * 1000);
                    }
                    else
                    {
                        Console.WriteLine($"Server signaled, waiting for any key ...");
                        // "Sleep" forever; do not use "Console.ReadKey()" here, because there might not be standard input.
                        do
                        {
                            Thread.Sleep(5000);
                        } while (true);
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }
}
