using System;
using System.IO;

namespace PaletteConverter
{
    public class Program
    {
        private static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                string appName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                Console.WriteLine($"Proper usage: {appName} <target>");
                return;
            }

            string filePath = Path.GetFullPath(args[0]);

            ImageProcessor.ProcessFiles(filePath);
        }
    }
}
