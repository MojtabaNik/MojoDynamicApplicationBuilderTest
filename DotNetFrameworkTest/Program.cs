using DotNetFrameworkDataLayer;
using System;
using System.IO;
using Console = System.Console;

namespace DotNetFrameworkTest
{
    class Program
    {
        static AppDbContext dbContext = new AppDbContext();
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Program is watching your directory, Ready to update your database as you insert your dlls");
            // Create a new FileSystemWatcher and set its properties.
            using (FileSystemWatcher watcher = new FileSystemWatcher())
            {
                watcher.Path = AppDomain.CurrentDomain.BaseDirectory;

                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                watcher.NotifyFilter = NotifyFilters.LastAccess
                                       | NotifyFilters.LastWrite
                                       | NotifyFilters.FileName
                                       | NotifyFilters.DirectoryName;

                // Only watch text files.
                watcher.Filter = "*.dll";

                // Add event handlers.
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Deleted += OnChanged;

                // Begin watching.
                watcher.EnableRaisingEvents = true;

                Console.ForegroundColor = ConsoleColor.Red;
                // Wait for the user to quit the program.
                Console.WriteLine("Press 'q' to quit the sample.");
                Console.ForegroundColor = ConsoleColor.Gray;

                //Refresh Database for the first run
                dbContext.RefreshDb();

                while (Console.Read() != 'q') ;
            }
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            dbContext.RefreshDb();
        }


    }
}
