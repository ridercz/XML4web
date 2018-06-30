using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using HtmlAgilityPack;

namespace Altairis.Xml4web.Compiler {
    class Program {
        private const int ERRORLEVEL_SUCCESS = 0;
        private const int ERRORLEVEL_FAILURE = 1;
        private const string INPUT_FOLDER = "src";
        private const string OUTPUT_FOLDER = "site";
        private const string STATIC_FOLDER = "static";

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

            // Delete and copy needed files
            PrepareFileSystem();

            // Create site metadata document
            var metadataFileName = Path.Combine(config.FolderName, "metadata.xml");
            var metadataDocument = CreateMetadataDocument();
            metadataDocument.Save(metadataFileName);

            // Run transforms
            foreach (var transform in config.Transforms) {
                var templateFileName = Path.Combine(config.FolderName, transform.Key);
                if (!string.IsNullOrEmpty(transform.Value)) {
                    // Simple transform
                    var outputFileName = Path.Combine(config.FolderName, OUTPUT_FOLDER, transform.Value);
                    RunTransform(metadataDocument, templateFileName, outputFileName);
                }
                else {
                    // Multi-document transform
                    var outputFileName = Path.Combine(config.FolderName, OUTPUT_FOLDER, Guid.NewGuid().ToString() + ".xml");
                    RunTransform(metadataDocument, templateFileName, outputFileName);
                    SplitFile(outputFileName, templateFileName + ".log");
                    File.Delete(outputFileName);
                }

            }
        }

        private static void SplitFile(string inputFileName, string errorLogFile) {
            Console.WriteLine("Splitting file:");
            var multiDoc = new HtmlDocument();
            multiDoc.Load(inputFileName);
            multiDoc.OptionOutputAsXml = true;
            multiDoc.OptionAutoCloseOnEnd = true;
            var documentNodes = multiDoc.DocumentNode.SelectNodes("/*/document");
            foreach (var item in documentNodes) {
                var href = item.GetAttributeValue("href", null).Trim('/', '\\');
                if (string.IsNullOrWhiteSpace(href)) continue;
                try {
                    Console.Write($"  {href}...");
                    var fileName = Path.Combine(config.FolderName, OUTPUT_FOLDER, href);
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    File.WriteAllText(fileName, item.InnerHtml);
                    Console.WriteLine("OK");
                }
                catch (Exception ex) {
                    Console.WriteLine("Failed!");
                    Console.WriteLine(ex.Message);
                    File.AppendAllText(errorLogFile, $"\r\n{href}\r\n{ex}");
                }
            }
        }

        private static void PrepareFileSystem() {
            // Delete log files
            try {
                Console.Write("Deleting old log files...");
                foreach (var item in Directory.GetFiles(config.FolderName, "*.log")) {
                    File.Delete(item);
                }
                Console.WriteLine("OK");
            }
            catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }

            // Delete output folder
            var outputFolder = Path.Combine(config.FolderName, OUTPUT_FOLDER);
            if (Directory.Exists(outputFolder)) {
                Console.Write($"Deleting {outputFolder}...");
                try {
                    var sw = new Stopwatch();
                    sw.Start();
                    Directory.Delete(outputFolder, recursive: true);
                    sw.Stop();
                    Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception ex) {
                    Console.WriteLine("Failed!");
                    Console.WriteLine(ex.Message);
                    Environment.Exit(ERRORLEVEL_FAILURE);
                }
            }

            // Copy static data to output folder
            var staticFolder = Path.Combine(config.FolderName, STATIC_FOLDER);
            if (Directory.Exists(staticFolder)) {
                Console.Write($"Copying {staticFolder} to {outputFolder}...");
                try {
                    var sw = new Stopwatch();
                    sw.Start();
                    DirectoryCopy(staticFolder, outputFolder, copySubDirs: true);
                    sw.Stop();
                    Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception ex) {
                    Console.WriteLine("Failed!");
                    Console.WriteLine(ex.Message);
                    Environment.Exit(ERRORLEVEL_FAILURE);
                }
            }
        }

        private static void RunTransform(IXPathNavigable metadataDocument, string templateFileName, string outputFileName) {
            Console.Write($"Running transformation {Path.GetFileName(templateFileName)}...");
            try {
                var sw = new Stopwatch();
                sw.Start();

                // Create output directory
                Directory.CreateDirectory(Path.GetDirectoryName(outputFileName));

                // Prepare transformation
                var args = new XsltArgumentList();
                args.AddExtensionObject(Namespaces.X4H, new XsltHelper(config.FolderName));

                var tran = new XslCompiledTransform();
                tran.Load(templateFileName, XsltSettings.TrustedXslt, new XmlUrlResolver());

                // Run transformation
                using (var writer = File.CreateText(outputFileName)) {
                    tran.Transform(metadataDocument, args, writer);
                }

                sw.Stop();
                Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine($"For details see {templateFileName}.log");
                File.WriteAllText(templateFileName + ".log", ex.ToString());
            }
        }

        private static SiteMetadataDocument CreateMetadataDocument() {
            Console.Write("Creating metadata document...");
            var sw = new Stopwatch();
            sw.Start();
            var doc = SiteMetadataDocument.CreateFromFolder(config.FolderName);
            sw.Stop();

            if (doc.Errors.Any()) {
                Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms with {doc.Errors.Count()} errors, see metadata.xml.log for details.");
                File.WriteAllLines(Path.Combine(config.FolderName, "metadata.xml.log"), doc.Errors.Select(x => string.Join('\t', x.Key, x.Value)));
            }
            else {
                Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
            }
            return doc;
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs) {
            // Get the subdirectories for the specified directory.
            var dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists) {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            var dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName)) {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            var files = dir.GetFiles();
            foreach (FileInfo file in files) {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs) {
                foreach (DirectoryInfo subdir in dirs) {
                    var temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

    }
}
