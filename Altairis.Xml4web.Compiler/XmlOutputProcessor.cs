using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Altairis.Xml4web.Compiler {
    public class XmlOutputProcessor {
        readonly XmlDocument _mainDoc;
        readonly string _baseFolder;
        readonly string _prependDoctype;
        readonly XmlNamespaceManager _nsmgr;

        public XmlOutputProcessor(string mainDocName, string baseFolder, string prependDoctype) {
            _mainDoc = new XmlDocument();
            _mainDoc.Load(mainDocName);

            _nsmgr = new XmlNamespaceManager(_mainDoc.NameTable);
            _nsmgr.AddNamespace("x4o", Namespaces.X4O);

            _baseFolder = baseFolder;
            _prependDoctype = prependDoctype;
        }

        public void SaveAllFiles(string mainFileName) {
            // Check if there are multiple output documents
            var outputDocuments = _mainDoc.SelectNodes("/x4o:root/x4o:document", this._nsmgr);

            if (outputDocuments.Count == 0) {
                // Single document
                this.SaveFile(_mainDoc, mainFileName);
            }
            else {
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

            foreach (XmlElement item in doc.SelectNodes("//*[@x4o:unescape='true']", _nsmgr)) {
                item.RemoveAttribute("unescape", Namespaces.X4O);
                var key = $"<!--REPLACE:{Guid.NewGuid()}-->";
                replacements.Add(key, item.InnerText);
                item.InnerXml = key;
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_prependDoctype)) sb.AppendLine(_prependDoctype);
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

            fileName = Path.Combine(_baseFolder, fileName.Trim('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            File.WriteAllText(fileName, sb.ToString());
        }

    }
}
