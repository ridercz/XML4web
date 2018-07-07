using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;

namespace Altairis.Xml4Web.Importer.Nemesis {
    class Program {
        private const int ERRORLEVEL_SUCCESS = 0;
        private const int ERRORLEVEL_FAILURE = 1;

        private static ImportConfiguration config;
        private static DataTable articles;
        private static StringBuilder idMapSb = new StringBuilder();
        private static StringBuilder linkListSb = new StringBuilder();

        static void Main(string[] args) {
            Console.WriteLine("Altairis XML4web Importer from Nemesis Publishing");
            Console.WriteLine("Copyright (c) Michal A. Valášek - Altairis, 2018");
            Console.WriteLine();

            // Validate/load arguments
            if (args.Length != 2) {
                Console.WriteLine("USAGE: x4w-inp importscript.json");
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
            File.WriteAllText(Path.Combine(config.FolderName, "namespaces.txt"), Properties.Resources.Namespaces);
            Console.WriteLine("OK");

            // Create site metadata file
            if (config.SiteMetadata.Any()) {
                Console.Write("Saving site metadata...");
                var siteMetadataBuilder = new StringBuilder();
                foreach (var item in config.SiteMetadata) {
                    siteMetadataBuilder.AppendMetadataLine(item.Key, item.Value);
                }
                File.WriteAllText(Path.Combine(config.FolderName, "index.md"), siteMetadataBuilder.ToString());
                Console.WriteLine("OK");
            }

            // Load data from database
            Console.Write("Loading articles from database...");
            using (var da = new SqlDataAdapter(Properties.Resources.ArticleDump, config.ConnectionString)) {
                articles = new DataTable();
                da.Fill(articles);
            }
            Console.WriteLine($"OK, {articles.Rows.Count} items");

            // Process all rows
            var importedCount = 0;
            var skippedCount = 0;
            foreach (DataRow row in articles.Rows) {
                var newId = ProcessArticle(row);
                if (string.IsNullOrEmpty(newId)) {
                    skippedCount++;
                }
                else {
                    importedCount++;
                    idMapSb.AppendLine(string.Join('\t', row["ArticleId"], newId));
                }
            }

            // Save ID map file
            if (!string.IsNullOrEmpty(config.IdMapFileName)) {
                Console.Write("Saving idmap...");
                File.WriteAllText(Path.Combine(config.FolderName, config.IdMapFileName), idMapSb.ToString());
                Console.WriteLine("OK");
            }
            // Save Link list file
            if (!string.IsNullOrEmpty(config.IdMapFileName)) {
                Console.Write("Saving link list...");
                File.WriteAllText(Path.Combine(config.FolderName, config.LinkListFileName), linkListSb.ToString());
                Console.WriteLine("OK");
            }
            Console.WriteLine($"Import completed: {importedCount} articles imported, {skippedCount} skipped.");
        }

        private static string ProcessArticle(DataRow row) {
            Console.WriteLine($"Article #{row["ArticleId"]}: {row["Title"].ToString().ToSingleLine()}");

            // Check import exceptions
            var isPublished = row["DatePublished"] != DBNull.Value;
            if (config.ImportPublished && !isPublished) {
                Console.WriteLine("  Skipping unpublished article");
                return null;
            }
            if (config.ImportUnpublished && isPublished) {
                Console.WriteLine("  Skipping published article");
                return null;
            }
            var isExternal = !string.IsNullOrEmpty(row["AlternateUrl"].ToString());
            if (!config.ImportExternal && isExternal) {
                Console.WriteLine("  Skipping external article");
                return null;
            }

            // Create destination folder
            var newId = FormatDataString(config.FileNameFormat, row);
            Console.WriteLine($"  New ID: {newId}");
            var fileName = Path.Combine(config.FolderName, newId + ".md");

            // Create main file and add metadata
            Console.Write("  Constructing metadata...");
            var sb = new StringBuilder();
            sb.AppendMetadataLine("dcterms:identifier", FormatDataString(config.IdentifierFormat, row));
            sb.AppendMetadataLine("dcterms:title", row["Title"]);
            sb.AppendMetadataLine("dcterms:abstract", row["Abstract"]);
            sb.AppendMetadataLine("np9:categoryId", row["CategoryId"]);
            sb.AppendMetadataLine("x4w:category", row["CategoryName"]);
            sb.AppendMetadataLine("np9:authorId", row["AuthorId"]);
            sb.AppendMetadataLine("np9:authorEmail", row["AuthorEmail"]);
            sb.AppendMetadataLine("dcterms:creator", row["AuthorName"]);
            sb.AppendMetadataLine("np9:serialId", row["SerialId"]);
            sb.AppendMetadataLine("x4w:serial", row["SerialName"]);
            if (!(row["DateCreated"] is DBNull)) sb.AppendMetadataLine("dcterms:created", XmlConvert.ToString((DateTime)row["DateCreated"], XmlDateTimeSerializationMode.Local));
            if (!(row["DateUpdated"] is DBNull)) sb.AppendMetadataLine("dcterms:dateSubmitted", XmlConvert.ToString((DateTime)row["DateUpdated"], XmlDateTimeSerializationMode.Local));
            if (!(row["DatePublished"] is DBNull)) sb.AppendMetadataLine("dcterms:dateAccepted", XmlConvert.ToString((DateTime)row["DatePublished"], XmlDateTimeSerializationMode.Local));
            sb.AppendMetadataLine("x4w:alternateUrl", row["AlternateUrl"]);
            Console.WriteLine("OK");

            if (config.ImportPictures) {
                var pictureType = row["PictureContentType"].ToString();
                if (!string.IsNullOrWhiteSpace(pictureType)) {
                    var pictureData = row["PictureData"] as byte[];
                    sb.AppendMetadataLine("x4w:pictureWidth", row["PictureWidth"]);
                    sb.AppendMetadataLine("x4w:pictureHeight", row["PictureHeight"]);

                    if (string.IsNullOrEmpty(config.ImportPicturesPath) || string.IsNullOrEmpty(config.ImportPicturesUrl)) {
                        // Embed in metadata
                        var dataUri = $"data:{pictureType};base64,{Convert.ToBase64String(pictureData)}";
                        sb.AppendMetadataLine("x4w:pictureUrl", dataUri);
                        Console.WriteLine("  Embedding picture...OK");
                    }
                    else {
                        // Save to path
                        var pictureFileName = AddExtensionFromType(FormatDataString(config.ImportPicturesPath, row), pictureType);
                        var pictureUrl = AddExtensionFromType(FormatDataString(config.ImportPicturesUrl, row), pictureType);

                        Console.Write($"  Saving picture...");
                        Directory.CreateDirectory(Path.GetDirectoryName(pictureFileName));
                        File.WriteAllBytes(pictureFileName, pictureData);
                        sb.AppendMetadataLine("x4w:pictureUrl", pictureUrl);
                        Console.WriteLine("OK");
                    }
                }
            }

            // Analyze links
            var html = row["Body"].ToString();
            html = ProcessLinks(newId, html);

            // Add body
            sb.AppendLine();
            if (config.ConvertHtmlToMarkdown) {
                Console.Write("  Converting to Markdown...");
                try {
                    var mdc = new Html2Markdown.Converter();
                    var md = mdc.Convert(html.ToSingleLine());
                    sb.Append(md);
                    Console.WriteLine("OK");
                }
                catch (Exception) {
                    Console.WriteLine("Failed, fallback to HTML");
                }
            }
            else {
                sb.Append(html);
            }

            // Save file
            Console.Write("  Saving file...");
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            File.WriteAllText(fileName, sb.ToString());
            Console.WriteLine("OK");

            return newId;
        }

        private static string ProcessLinks(string newId, string html) {
            if (string.IsNullOrWhiteSpace(config.LinkListFileName) && !config.LinkReplacements.Any()) return html;

            Console.Write("  Analyzing links...");

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var foundCounter = 0;

            var aHrefNodes = doc.DocumentNode.SelectNodes("//a");
            if (aHrefNodes != null) {
                foreach (var item in aHrefNodes) {
                    var val = item.GetAttributeValue("href", null);
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    val = val.Replace("&amp;", "&");

                    var newVal = ReplaceLink(val);
                    item.SetAttributeValue("href", newVal);

                    linkListSb.AppendLine(string.Join('\t', newId, "a_href", val, newVal));
                    foundCounter++;
                }
            }

            var imgSrcNodes = doc.DocumentNode.SelectNodes("//img");
            if (imgSrcNodes != null) {
                foreach (var item in imgSrcNodes) {
                    var val = item.GetAttributeValue("src", null);
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    val = val.Replace("&amp;", "&");

                    var newVal = ReplaceLink(val);
                    item.SetAttributeValue("src", newVal);

                    linkListSb.AppendLine(string.Join('\t', newId, "img_src", val, newVal));
                    foundCounter++;
                }
            }

            if (!string.IsNullOrWhiteSpace(config.LinkListFileName) && foundCounter > 0) {
                var fileName = Path.Combine(config.FolderName, config.LinkListFileName);
                File.AppendAllText(fileName, linkListSb.ToString());
            }

            Console.WriteLine($"OK, found {foundCounter} links");

            return doc.DocumentNode.InnerHtml;
        }

        private static string ReplaceLink(string url) {
            foreach (var item in config.LinkReplacements) {
                if (Regex.IsMatch(url, item.Key, RegexOptions.IgnoreCase)) return Regex.Replace(url, item.Key, item.Value, RegexOptions.IgnoreCase);
            }
            return url;
        }

        private static string AddExtensionFromType(string s, string contentType) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(s));
            if (contentType == null) throw new ArgumentNullException(nameof(contentType));
            if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(contentType));

            if (!s.EndsWith(".*", StringComparison.Ordinal)) return s;
            s = s.Substring(0, s.Length - 1);

            if (contentType.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase) || contentType.EndsWith("jpg", StringComparison.OrdinalIgnoreCase)) {
                s += "jpg";
            }
            else if (contentType.EndsWith("png", StringComparison.OrdinalIgnoreCase)) {
                s += "png";
            }
            else {
                s += "bin";
            }

            return s;
        }

        private static string FormatDataString(string s, DataRow row) {
            return Regex.Replace(s, @"\$\(([^)]+)\)", (m) => {
                var placeholder = m.Groups[1].Value.Split(':', 2);
                if (placeholder.Length == 1) {
                    return row[placeholder[0]].ToString().ToUrlKey();
                }
                else {
                    var formattable = row[placeholder[0]] as IFormattable;
                    return formattable.ToString(placeholder[1], CultureInfo.InvariantCulture).ToUrlKey();
                }
            });
        }
    }
}
