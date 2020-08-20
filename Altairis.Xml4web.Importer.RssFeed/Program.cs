using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Altairis.Xml4web.Importer.RssFeed {
    internal class Program {
        private const int ERRORLEVEL_SUCCESS = 0;
        private const int ERRORLEVEL_FAILURE = 1;

        private static ImportConfiguration config;
        private static XmlNamespaceManager nsmgr;

        private static void Main(string[] args) {
            Console.WriteLine("Altairis XML4web Importer from RSS Feed");
            Console.WriteLine("Copyright (c) Michal A. Valášek - Altairis, 2018");
            Console.WriteLine();

            // Validate/load arguments
            if (args.Length != 2) {
                Console.WriteLine("USAGE: x4w-irss importscript.json");
                Environment.Exit(ERRORLEVEL_SUCCESS);
            }

            var importScriptFileName = args[1];
            if (!File.Exists(importScriptFileName)) {
                Console.WriteLine($"ERROR: File '{importScriptFileName}' was not found!");
                Environment.Exit(ERRORLEVEL_FAILURE);
            }

            // Load configuration
            Console.Write("Loading configuration...");
            config = ImportConfiguration.Load(importScriptFileName);
            Console.WriteLine("OK");

            // Create output directory and copy namespaces file
            Console.Write($"Creating folder {config.FolderName}...");
            Directory.CreateDirectory(config.FolderName);
            Console.WriteLine("OK");

            // Load RSS feed
            Console.WriteLine($"Loading RSS from {config.RssUrl}...");
            var doc = new XmlDocument();
            try {
                doc.Load(config.RssUrl);
                Console.WriteLine("OK");
            } catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }

            // Analyze namespaces
            Console.WriteLine("Loading XML namespaces...");
            nsmgr = new XmlNamespaceManager(doc.NameTable);
            foreach (XmlAttribute attr in doc.DocumentElement.Attributes) {
                if (attr.Name == "xmlns") {
                    nsmgr.AddNamespace(string.Empty, attr.Value);
                    Console.WriteLine($"  Default namespace = {attr.Value}");
                } else if (attr.Prefix == "xmlns") {
                    nsmgr.AddNamespace(attr.LocalName, attr.Value);
                    Console.WriteLine($"  {attr.LocalName} = {attr.Value}");
                }
            }

            // Select items
            var items = doc.SelectNodes(config.ItemXPath, nsmgr);
            Console.WriteLine($"Found {items.Count} items.");

            // Process all items
            var counter = 0;
            foreach (XmlElement item in items) {
                counter++;
                Console.WriteLine($"Processing item #{counter}:");
                ProcessFeedItem(item);
            }
        }

        private static void ProcessFeedItem(XmlElement feedItem) {
            Console.WriteLine("  Reading metadata:");
            // Get article metadata from config and XML
            var metadata = GetArticleMetadata(feedItem);

            // Parse publication date
            var dateAccepted = DateTime.Parse(GetValueFromNode(feedItem.SelectSingleNode(config.DateXPath)), CultureInfo.GetCultureInfo(config.DateLocale));
            metadata.Add("dcterms:dateAccepted", dateAccepted);
            Console.WriteLine($"    dcterms:dateAccepted = {XmlConvert.ToString(dateAccepted, XmlDateTimeSerializationMode.RoundtripKind)}");

            // Dump metadata
            var sb = new StringBuilder();
            foreach (var item in metadata) {
                sb.AppendMetadataLine(item.Key, item.Value);
            }

            // Create file name
            var fileName = Path.Combine(config.FolderName, FormatDataString(config.FileName, metadata));
            Console.Write($"  Saving {fileName}...");
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                File.WriteAllText(fileName, sb.ToString());
            } catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static Dictionary<string, object> GetArticleMetadata(XmlElement feedItem) {
            var metadata = new Dictionary<string, object>();

            // Load metadata from XML
            foreach (var metadataLocator in config.ImportMetadataFromXPath) {
                Console.Write($"    {metadataLocator.Key} = ");
                var metadataNode = feedItem.SelectSingleNode(metadataLocator.Value, nsmgr);
                var metadataValue = GetValueFromNode(metadataNode);

                if (string.IsNullOrEmpty(metadataValue)) {
                    Console.WriteLine("null or empty, skipping");
                    continue;
                }
                metadataValue = System.Web.HttpUtility.HtmlDecode(metadataValue);
                metadataValue = Regex.Replace(metadataValue, "<[^>]*>", string.Empty);

                Console.WriteLine(metadataValue);
                metadata.Add(metadataLocator.Key, metadataValue);

            }

            // Append static metadata
            foreach (var item in config.StaticMetadata) {
                Console.WriteLine($"    {item.Key} = {item.Value}");
                metadata.Add(item.Key, item.Value);
            }

            return metadata;
        }

        private static string GetValueFromNode(XmlNode node) {
            if (node is null) return null;
            if (node is XmlElement) return node.InnerText;
            if (node is XmlAttribute) return node.Value;
            return null;
        }

        private static string FormatDataString(string s, IDictionary<string, object> metadata) {
            return Regex.Replace(s, @"\$\(([^)]+)\)", (m) => {
                var placeholder = m.Groups[1].Value.Split('|', 2);
                if (placeholder.Length == 1) {
                    return metadata[placeholder[0]].ToString().ToUrlKey();
                } else {
                    var formattable = metadata[placeholder[0]] as IFormattable;
                    return formattable.ToString(placeholder[1], CultureInfo.InvariantCulture).ToUrlKey();
                }
            });
        }
    }
}
