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
        public const int ERRORLEVEL_SUCCESS = 0;
        public const int ERRORLEVEL_FAILURE = 1;

        private static BuildConfiguration _config;

        static void Main(string[] args) {
            Console.WriteLine("Altairis XML4web Site Compiler");
            Console.WriteLine("Copyright (c) Michal A. Valášek - Altairis, 2018");
            Console.WriteLine();

            var tsw = new Stopwatch();
            tsw.Start();

            LoadConfiguration(args);

            // Delete and copy needed files
            PrepareFileSystem();

            // Create site metadata document
            var metadataFileName = Path.Combine(_config.WorkFolder, "metadata.xml");
            var metadataDocument = CreateMetadataDocument();
            metadataDocument.Save(metadataFileName);

            // Run transforms
            RunAllTransforms(metadataDocument);

            // Check if there are some errors
            tsw.Stop();
            var logFiles = Directory.GetFiles(_config.WorkFolder, "*.log");
            if (!logFiles.Any()) {
                Console.WriteLine($"Build completed successfully in {tsw.ElapsedMilliseconds} ms.");
                Environment.Exit(ERRORLEVEL_SUCCESS);
            }
            else {
                Console.WriteLine($"Build failed in {tsw.ElapsedMilliseconds} ms. See the following log files:");
                Console.WriteLine(string.Join(Environment.NewLine, logFiles));
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static void LoadConfiguration(string[] args) {
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
            try {
                _config = BuildConfiguration.Load(buildScriptFileName);
                Console.WriteLine("OK");
            }
            catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static void PrepareFileSystem() {
            // Delete target and work folder
            FileSystemHelper.DirectoryDelete(_config.TargetFolder);
            Directory.CreateDirectory(_config.TargetFolder);
            FileSystemHelper.DirectoryDelete(_config.WorkFolder);
            Directory.CreateDirectory(_config.WorkFolder);

            // Copy static data to output folder
            if (Directory.Exists(_config.StaticFolder)) {
                Console.Write($"Copying {_config.StaticFolder} to {_config.TargetFolder}...");
                try {
                    var sw = new Stopwatch();
                    sw.Start();
                    FileSystemHelper.DirectoryCopy(_config.StaticFolder, _config.TargetFolder);
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

        private static void RunAllTransforms(SiteMetadataDocument metadataDocument) {
            Console.WriteLine("Running HTML transformations:");
            foreach (var transform in _config.HtmlTransforms) {
                var templateFileName = Path.Combine(_config.XsltFolder, transform.Key);
                var outputFileName = Path.Combine(_config.WorkFolder, Path.GetFileNameWithoutExtension(transform.Key) + ".xml");

                RunTransform(metadataDocument, templateFileName, outputFileName);

                Console.Write("  Running post-processor...");
                var proc = new XmlOutputProcessor(outputFileName, _config.TargetFolder, _config.PrependHtmlDoctype);
                proc.SaveAllFiles(transform.Value);
                Console.WriteLine("OK");
            }

            // Run raw transforms
            Console.WriteLine("Running raw transformations:");
            foreach (var transform in _config.RawTransforms) {
                var templateFileName = Path.Combine(_config.XsltFolder, transform.Key);
                var outputFileName = Path.Combine(_config.TargetFolder, transform.Value);

                RunTransform(metadataDocument, templateFileName, outputFileName);
            }
        }

        private static void RunTransform(IXPathNavigable metadataDocument, string templateFileName, string outputFileName) {
            Console.Write($"  Running {Path.GetFileName(templateFileName)}...");
            try {
                var sw = new Stopwatch();
                sw.Start();

                // Create output directory
                Directory.CreateDirectory(Path.GetDirectoryName(outputFileName));

                // Prepare transformation
                var args = new XsltArgumentList();
                args.AddExtensionObject(Namespaces.X4H, new XsltHelper(_config));
                foreach (var item in _config.TransformParameters) {
                    args.AddParam(item.Key, Namespaces.X4C, item.Value);
                }

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

    }
}
