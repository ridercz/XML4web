using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace Altairis.Xml4web.Compiler {
    class Program {
        private const int ERRORLEVEL_SUCCESS = 0;
        private const int ERRORLEVEL_FAILURE = 1;

        private static BuildConfiguration config;

        static void Main(string[] args) {
            Console.WriteLine("Altairis XML4web Site Compiler");
            Console.WriteLine("Copyright (c) Michal A. Valášek - Altairis, 2018");
            Console.WriteLine();

            // Validate/load arguments
            if (args.Length != 2) {
                Console.WriteLine("USAGE: x4w-compiler buildscript.json");
                Environment.Exit(ERRORLEVEL_SUCCESS);
            }

            var buildScriptFileName = args[1];
            if (!File.Exists(buildScriptFileName)) {
                Console.WriteLine($"ERROR: File '{buildScriptFileName}' was not found!");
                Environment.Exit(ERRORLEVEL_FAILURE);
            }

            // Load configuration
            Console.Write("Loading configuration...");
            config = BuildConfiguration.Load(buildScriptFileName);
            Console.WriteLine("OK");

            // Create site metadata document
            Console.Write("Creating metadata document...");
            File.Delete(Path.Combine(config.FolderName, "metadata.log"));
            var doc = SiteMetadataDocument.CreateFromFolder(config.FolderName);
            doc.Save(Path.Combine(config.FolderName, "metadata.xml"));
            if (doc.Errors.Any()) {
                Console.WriteLine($"Done with {doc.Errors.Count()} errors, see metadata.log for details.");
                File.WriteAllLines(Path.Combine(config.FolderName, "metadata.log"), doc.Errors.Select(x => string.Join('\t', x.Key, x.Value)));
            }
            else {
                Console.WriteLine("OK");
            }
        }
    }
}
