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

        private static BuildConfiguration _config;

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
            var tsw = new Stopwatch();
            tsw.Start();


            // Load configuration
            Console.Write("Loading configuration...");
            _config = BuildConfiguration.Load(buildScriptFileName);
            Console.WriteLine("OK");

            // Delete and copy needed files
            PrepareFileSystem();

            // Create site metadata document
            var metadataFileName = Path.Combine(_config.WorkFolder, "metadata.xml");
            var metadataDocument = CreateMetadataDocument();
            metadataDocument.Save(metadataFileName);

            // Run transforms
            foreach (var transform in _config.Transforms) {
                var templateFileName = Path.Combine(_config.XsltFolder, transform.Key);
                if (!string.IsNullOrEmpty(transform.Value)) {
                    // Simple transform
                    var outputFileName = Path.Combine(_config.TargetFolder, transform.Value);
                    RunTransform(metadataDocument, templateFileName, outputFileName);
                }
                else {
                    // Multi-document transform
                    var outputFileName = Path.Combine(_config.TargetFolder, Guid.NewGuid().ToString() + ".xml");
                    RunTransform(metadataDocument, templateFileName, outputFileName);
                    SplitFile(outputFileName, templateFileName + ".log");
                    File.Delete(outputFileName);
                }
            }

            // Check if there are some errors
            tsw.Stop();
            var logFiles = Directory.GetFiles(_config.WorkFolder, "*.log");
            if (!logFiles.Any()) {
                Console.WriteLine($"Build completed successfully in {tsw.ElapsedMilliseconds} ms.");
            }
            else {
                Console.WriteLine($"Build failed in {tsw.ElapsedMilliseconds} ms. See the following log files:");
                Console.WriteLine(logFiles.Select(s => "  " + s + Environment.NewLine));
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
                    var fileName = Path.Combine(_config.TargetFolder, href);
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
            // Delete target and work folder
            DirectoryDelete(_config.TargetFolder);
            Directory.CreateDirectory(_config.TargetFolder);
            DirectoryDelete(_config.WorkFolder);
            Directory.CreateDirectory(_config.WorkFolder);

            // Copy static data to output folder
            if (Directory.Exists(_config.StaticFolder)) {
                Console.Write($"Copying {_config.StaticFolder} to {_config.TargetFolder}...");
                try {
                    var sw = new Stopwatch();
                    sw.Start();
                    DirectoryCopy(_config.StaticFolder, _config.TargetFolder);
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
                args.AddExtensionObject(Namespaces.X4H, new XsltHelper(_config));

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
                var errorLogName = Path.Combine(_config.WorkFolder, Path.GetFileName(templateFileName) + ".log");
                Console.WriteLine($"For details see {errorLogName}");
                File.WriteAllText(errorLogName, ex.ToString());
            }
        }

        private static SiteMetadataDocument CreateMetadataDocument() {
            Console.Write("Creating metadata document...");
            var sw = new Stopwatch();
            sw.Start();
            var doc = SiteMetadataDocument.CreateFromFolder(_config.SourceFolder);
            sw.Stop();

            if (doc.Errors.Any()) {
                Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms with {doc.Errors.Count()} errors, see metadata.xml.log for details.");
                File.WriteAllLines(Path.Combine(_config.WorkFolder, "metadata.xml.log"), doc.Errors.Select(x => string.Join('\t', x.Key, x.Value)));
            }
            else {
                Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
            }
            return doc;
        }

        private static void DirectoryDelete(string folderName) {
            if (Directory.Exists(folderName)) {
                Console.Write($"Deleting {folderName}...");
                try {
                    var sw = new Stopwatch();
                    sw.Start();
                    Directory.Delete(folderName, recursive: true);
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

        private static void DirectoryCopy(string sourcePath, string targetPath) {
            var sourceDirectory = new DirectoryInfo(sourcePath);
            if (!sourceDirectory.Exists) throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourcePath);

            // Copy files
            Directory.CreateDirectory(targetPath);
            foreach (var f in sourceDirectory.GetFiles()) {
                f.CopyTo(Path.Combine(targetPath, f.Name), overwrite: true);
            }

            // Copy directories
            foreach (var d in sourceDirectory.GetDirectories()) {
                DirectoryCopy(d.FullName, Path.Combine(targetPath, d.Name));
            }

        }

    }
}
