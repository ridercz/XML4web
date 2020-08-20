using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Altairis.Xml4web.Compiler {
    internal class Program {
        public const int ERRORLEVEL_SUCCESS = 0;
        public const int ERRORLEVEL_FAILURE = 1;

        private static BuildConfiguration config;

        private static void Main(string[] args) {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine();
            Console.WriteLine($@"o   o o   o o    o  o                o     XML4web Static Site Generator");
            Console.WriteLine($@" \ /  |\ /| |    |  |                |     Version {version}");
            Console.WriteLine($@"  O   | O | |    o--O o   o   o o-o  O-o   Copyright (c) 2018-2020");
            Console.WriteLine($@" / \  |   | |       |  \ / \ /  |-'  |  |  Michal A. Valášek - Altairis");
            Console.WriteLine($@"o   o o   o O---o   o   o   o   o-o  o-o   www.xml4web.com | www.rider.cz");
            Console.WriteLine();

            var tsw = new Stopwatch();
            tsw.Start();

            LoadConfiguration(args);

            // Delete and copy needed files
            PrepareFileSystem();

            // Create site metadata document
            var metadataFileName = Path.Combine(config.WorkFolder, "metadata.xml");
            var metadataDocument = CreateMetadataDocument();
            metadataDocument.Save(metadataFileName);

            // Run transforms
            RunAllTransforms(metadataDocument);

            // Check if there are some errors
            tsw.Stop();
            var logFiles = Directory.GetFiles(config.WorkFolder, "*.log");
            if (!logFiles.Any()) {
                Console.WriteLine($"Build completed successfully in {tsw.ElapsedMilliseconds} ms.");
                Environment.Exit(ERRORLEVEL_SUCCESS);
            } else {
                Console.WriteLine($"Build failed in {tsw.ElapsedMilliseconds} ms. See the following log files:");
                Console.WriteLine(string.Join(Environment.NewLine, logFiles));
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static void LoadConfiguration(string[] args) {
            // Validate/load arguments
            if (args.Length != 1) {
                Console.WriteLine("USAGE: x4w-compiler buildscript.json");
                Environment.Exit(ERRORLEVEL_SUCCESS);
            }

            var buildScriptFileName = args[0];
            if (!File.Exists(buildScriptFileName)) {
                Console.WriteLine($"ERROR: File '{buildScriptFileName}' was not found!");
                Environment.Exit(ERRORLEVEL_FAILURE);
            }

            // Load configuration
            Console.Write("Loading configuration...");
            try {
                config = BuildConfiguration.Load(buildScriptFileName);
                Console.WriteLine("OK");
            } catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static void PrepareFileSystem() {
            // Delete target and work folder
            FileSystemHelper.DirectoryDelete(config.TargetFolder);
            Directory.CreateDirectory(config.TargetFolder);
            FileSystemHelper.DirectoryDelete(config.WorkFolder);
            Directory.CreateDirectory(config.WorkFolder);

            // Copy static data to output folder
            if (Directory.Exists(config.StaticFolder)) {
                Console.Write($"Copying {config.StaticFolder} to {config.TargetFolder}...");
                try {
                    var sw = new Stopwatch();
                    sw.Start();
                    FileSystemHelper.DirectoryCopy(config.StaticFolder, config.TargetFolder);
                    sw.Stop();
                    Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
                } catch (Exception ex) {
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
            var doc = SiteMetadataDocument.CreateFromFolder(config.SourceFolder);
            sw.Stop();

            if (doc.Errors.Any()) {
                Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms with {doc.Errors.Count()} errors, see metadata.xml.log for details.");
                File.WriteAllLines(Path.Combine(config.WorkFolder, "metadata.xml.log"), doc.Errors.Select(x => string.Join("\t", x.Key, x.Value)));
            } else {
                Console.WriteLine($"OK in {sw.ElapsedMilliseconds} ms");
            }
            return doc;
        }

        private static void RunAllTransforms(SiteMetadataDocument metadataDocument) {
            Console.WriteLine("Running HTML transformations:");
            foreach (var transform in config.HtmlTransforms) {
                var templateFileName = Path.Combine(config.XsltFolder, transform.Key);
                var outputFileName = Path.Combine(config.WorkFolder, Path.GetFileNameWithoutExtension(transform.Key) + ".xml");

                RunTransform(metadataDocument, templateFileName, outputFileName);

                Console.Write("  Running post-processor...");
                var proc = new XmlOutputProcessor(outputFileName, config.TargetFolder);
                proc.SaveAllFiles(transform.Value);
                Console.WriteLine("OK");
            }

            // Run raw transforms
            Console.WriteLine("Running raw transformations:");
            foreach (var transform in config.RawTransforms) {
                var templateFileName = Path.Combine(config.XsltFolder, transform.Key);
                var outputFileName = Path.Combine(config.TargetFolder, transform.Value);

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
                args.AddExtensionObject(Namespaces.X4H, new XsltHelper(config));
                foreach (var item in config.TransformParameters) {
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
            } catch (Exception ex) {
                Console.WriteLine("Failed!");
                var errorLogName = Path.Combine(config.WorkFolder, Path.GetFileName(templateFileName) + ".log");
                Console.WriteLine($"For details see {errorLogName}");
                File.WriteAllText(errorLogName, ex.ToString());
            }
        }

    }
}
