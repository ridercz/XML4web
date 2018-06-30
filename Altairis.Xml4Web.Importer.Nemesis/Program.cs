﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Altairis.Xml4Web.Importer.Nemesis {
    class Program {
        private const int ERRORLEVEL_SUCCESS = 0;
        private const int ERRORLEVEL_FAILURE = 1;

        private static ImportConfiguration config;
        private static DataTable articles;

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

            // Load data from database
            Console.Write("Loading articles from database...");

            using (var da = new SqlDataAdapter(Properties.Resources.ArticleDump, config.ConnectionString)) {
                articles = new DataTable();
                da.Fill(articles);
            }
            Console.WriteLine($"OK, {articles.Rows.Count} items");

            // Process all rows
            var idMapSb = new StringBuilder();
            foreach (DataRow row in articles.Rows) {
                var newId = ProcessArticle(row);
                idMapSb.AppendLine(string.Join('\t', row["ArticleId"], newId));
            }

            // Create ID map file
            if (!string.IsNullOrEmpty(config.IdMapFileName)) {
                Console.Write("Saving idmap...");
                File.WriteAllText(Path.Combine(config.FolderName, config.IdMapFileName), idMapSb.ToString());
                Console.WriteLine("OK");
            }
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
            if (config.ImportExternal && isExternal) {
                Console.WriteLine("  Skipping external article");
                return null;
            }

            // Create destination folder
            var newId = FormatDataString(config.FileNameFormat, row);
            Console.WriteLine($"  New ID: {newId}");
            var folderName = Path.Combine(config.FolderName, newId);
            Directory.CreateDirectory(folderName);

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

            // Create image
            var pictureType = row["PictureContentType"].ToString();
            if (!string.IsNullOrWhiteSpace(pictureType)) {
                Console.Write("  Saving picture...");
                var pictureData = row["PictureData"] as byte[];
                var pictureName = "picture.";
                if (pictureType.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase)) {
                    pictureName += "jpg";
                }
                else {
                    pictureName += pictureType.Substring(pictureType.LastIndexOf('/') + 1);
                }

                File.WriteAllBytes(Path.Combine(folderName, pictureName), pictureData);
                sb.AppendMetadataLine("x4w:picture", pictureName);
                sb.AppendMetadataLine("x4w:pictureWidth", row["PictureWidth"]);
                sb.AppendMetadataLine("x4w:pictureHeight", row["PictureHeight"]);
                Console.WriteLine(pictureName);
            }

            // Add body
            sb.AppendLine();
            var html = row["Body"].ToString();
            if (config.ConvertHtmlToMarkdown) {
                Console.Write("Converting to Markdown...");
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
            Console.Write("  Saving main file...");
            File.WriteAllText(Path.Combine(folderName, "index.md"), sb.ToString());
            Console.WriteLine("OK");

            return newId;
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
