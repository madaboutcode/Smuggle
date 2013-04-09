using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Smuggle
{
    internal class Program
    {
        private static OptionSet _options = new OptionSet
            {
                {"p=|pattern=", s => _pattern = s},
                {"s=|source=", s => _source = s},
                {"d=|dest=", s => _dest = s},
                {"h|?|help", v =>
                    {
                        ShowHelp();
                        Environment.Exit(0);
                    }
                }
            };

        private static string _source;
        private static string _pattern = @"^.*\.(dll|pdb)";
        private static string _dest;


        private static int Main(string[] args)
        {
            var extra = new Queue<string>(_options.Parse(args));

            if (extra.Count > 0 && string.IsNullOrEmpty(_dest))
            {
                _dest = extra.Dequeue();
            }

            if (extra.Count > 0 && string.IsNullOrEmpty(_source))
            {
                _source = extra.Dequeue();
            }

            if (extra.Count > 0)
            {
                Console.WriteLine("Invalid arguments - {0}", string.Join(" ", extra));
                ShowHelp();
                return -1;
            }

            if (string.IsNullOrEmpty(_source))
            {
                _source = Directory.GetCurrentDirectory();
            }

            CopyInitial();

            StartStreaming();

            Console.WriteLine("Press ESC to exit...");

            while (Console.ReadKey().Key != ConsoleKey.Escape)
            {
            }

            _watcher.Dispose();

            return 0;
        }

        private static FileSystemWatcher _watcher;

        private static void StartStreaming()
        {
            _watcher = new FileSystemWatcher(_source, "*.*");
            _watcher.Changed += (s, e) => CopyFile(e.FullPath);
            _watcher.Created += (s, e) => CopyFile(e.FullPath);
            _watcher.Deleted += (s, e) => CopyFile(e.FullPath);
            _watcher.EnableRaisingEvents = true;

            Task.Factory.StartNew(Worker);
        }

        static readonly ConcurrentQueue<string> JobQueue = new ConcurrentQueue<string>();  

        private static void CopyFile(string fullPath)
        {
            JobQueue.Enqueue(fullPath);
        }

        private static void Worker()
        {
            while (true)
            {
                Thread.Sleep(500);
                string fullPath;
                var filesToProcess = new HashSet<string>();
                while (JobQueue.TryDequeue(out fullPath))
                {
                    filesToProcess.Add(fullPath);
                }

                if(filesToProcess.Count == 0)
                    continue;

                Console.WriteLine("-----=[ Smuggling in progress ]=-----");

                foreach (var filePath in filesToProcess)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        Debug.Assert(fileName != null, "fileName != null");
                        var destPath = Path.Combine(_dest, fileName);

                        if (File.Exists(filePath))
                        {
                            Console.WriteLine("{0}: Copying {1}", DateTime.Now.ToShortTimeString(), fileName);
                            File.Copy(filePath, destPath, true);
                        }
                        else if (File.Exists(destPath))
                        {
                            Console.WriteLine("{0}: Deleting {1}", DateTime.Now.ToShortTimeString(), fileName);
                            File.Delete(destPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0} - ERROR : {1}", filePath, ex.Message);
                        JobQueue.Enqueue(filePath);
                    }
                }
            }
        }

        private static void CopyInitial()
        {
        }

        private static void ShowHelp()
        {
            _options.WriteOptionDescriptions(Console.Out);
        }
    }
}