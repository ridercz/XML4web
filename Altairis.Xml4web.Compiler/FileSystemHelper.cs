using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altairis.Xml4web.Compiler {
    public static class FileSystemHelper {

        private const int FS_RETRY_COUNT = 10;
        private const int FS_RETRY_PAUSE = 500; //ms

        public static void DirectoryDelete(string folderName) {
            if (Directory.Exists(folderName)) {
                Console.Write($"Deleting {folderName}...");

                var sw = new Stopwatch();
                sw.Start();

                var remainingRetries = FS_RETRY_COUNT;
                while (true) {
                    try {
                        Directory.Delete(folderName, recursive: true);
                        break;
                    }
                    catch (IOException ex) {
                        Console.WriteLine("Failed!");
                        Console.WriteLine(ex.Message);

                        remainingRetries--;
                        if (remainingRetries == 0) Environment.Exit(Program.ERRORLEVEL_FAILURE);

                        Console.Write($"Retrying in {FS_RETRY_PAUSE} ms...");
                        Thread.Sleep(FS_RETRY_PAUSE);
                    }
                }

                sw.Stop();
                Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
            }
        }

        public static void DirectoryCopy(string sourcePath, string targetPath) {
            var sourceDirectory = new DirectoryInfo(sourcePath);
            if (!sourceDirectory.Exists) throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourcePath);

            // Copy files
            foreach (var f in sourceDirectory.GetFiles()) {
                var remainingRetries = FS_RETRY_COUNT;
                while (true) {
                    try {
                        Directory.CreateDirectory(targetPath);
                        f.CopyTo(Path.Combine(targetPath, f.Name), overwrite: true);
                        break;
                    }
                    catch (Exception ex) {
                        Console.WriteLine("Failed!");
                        Console.WriteLine(ex.Message);

                        remainingRetries--;
                        if (remainingRetries == 0) Environment.Exit(Program.ERRORLEVEL_FAILURE);

                        Console.Write($"Retrying in {FS_RETRY_PAUSE} ms...");
                        Thread.Sleep(FS_RETRY_PAUSE);
                    }
                }
            }

            // Copy directories
            foreach (var d in sourceDirectory.GetDirectories()) {
                DirectoryCopy(d.FullName, Path.Combine(targetPath, d.Name));
            }

        }

    }
}
