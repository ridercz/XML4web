using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Xml;

namespace Altairis.Xml4web.Compiler {
    public class XmlOutputProcessor {
        private readonly XmlDocument mainDoc;
        private readonly string baseFolder;
        private readonly XmlNamespaceManager nsmgr;

        public XmlOutputProcessor(string mainDocName, string baseFolder) {
            this.mainDoc = new XmlDocument();
            this.mainDoc.Load(mainDocName);

            this.nsmgr = new XmlNamespaceManager(this.mainDoc.NameTable);
            this.nsmgr.AddNamespace("x4o", Namespaces.X4O);

            this.baseFolder = baseFolder;
        }

        public void SaveAllFiles(string mainFileName) {
            // Check if there are multiple output documents
            var outputDocuments = this.mainDoc.SelectNodes("/x4o:root/x4o:document", this.nsmgr);

            if (outputDocuments.Count == 0) {
                // Single document
                this.SaveFile(this.mainDoc, mainFileName);
            } else {
                // Multiple documents
                foreach (XmlElement item in outputDocuments) {
                    var href = item.GetAttribute("href");
                    var doc = new XmlDocument();
                    doc.AppendChild(doc.ImportNode(item.FirstChild, deep: true));
                    this.SaveFile(doc, href);
                }
            }
        }

        private void SaveFile(XmlDocument doc, string fileName) {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(fileName));

            var replacements = new NameValueCollection();

            foreach (XmlElement item in doc.SelectNodes("//*[@x4o:unescape='true']", this.nsmgr)) {
                item.RemoveAttribute("unescape", Namespaces.X4O);
                var key = $"<!--REPLACE:{Guid.NewGuid()}-->";
                replacements.Add(key, item.InnerText);
                item.InnerXml = key;
            }

            var sb = new StringBuilder();
            if (doc.DocumentElement.LocalName.Equals("html", StringComparison.OrdinalIgnoreCase)) sb.AppendLine("<!DOCTYPE html>");
            var settings = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "  ",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = true
            };
            using (var sw = XmlWriter.Create(sb, settings)) {
                doc.Save(sw);
            }

            foreach (var key in replacements.AllKeys) {
                sb = sb.Replace(key, replacements[key]);
            }

            fileName = Path.Combine(this.baseFolder, fileName.Trim('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            File.WriteAllText(fileName, sb.ToString());
        }

    }
}
