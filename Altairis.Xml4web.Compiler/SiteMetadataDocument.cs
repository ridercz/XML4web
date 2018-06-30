using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Altairis.Xml4web.Compiler {
    public class SiteMetadataDocument : XmlDocument {
        XmlNamespaceManager _nsmgr;

        public ICollection<KeyValuePair<string, string>> Errors { get; }
        public string BasePath { get; private set; }

        public static SiteMetadataDocument CreateFromFolder(string folderName) {
            var doc = new SiteMetadataDocument(Path.Combine(folderName, "src\\namespaces.txt"));
            doc.BasePath = Path.Combine(folderName, "src").TrimEnd('\\');
            doc.AddPageMetadata(Path.Combine(folderName, "src"), doc.DocumentElement);
            return doc;
        }

        private SiteMetadataDocument(string namespaceFile) {
            if (namespaceFile == null) throw new ArgumentNullException(nameof(namespaceFile));
            if (string.IsNullOrWhiteSpace(namespaceFile)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(namespaceFile));

            // Create root element
            this.AppendChild(this.CreateElement("siteMetadata"));

            // Add default namespaces
            _nsmgr = new XmlNamespaceManager(this.NameTable);
            _nsmgr.AddNamespace("dcterms", "http://purl.org/dc/terms/");
            _nsmgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
            _nsmgr.AddNamespace("x4w", "http://schemas.altairis.cz/XML4web/PageMetadata/");

            // Add namespaces from file
            var lines = File.ReadAllLines(namespaceFile);
            foreach (var line in lines) {
                var data = line.Split(':', 2);
                if (data.Length != 2) continue;
                _nsmgr.AddNamespace(data[0], data[1]);
                this.DocumentElement.SetAttribute("xmlns:" + data[0], data[1]);
            }

            // Add creation date
            this.DocumentElement.AppendChild(this.CreateQualifiedElement("dcterms:created", DateTime.Now));

            // Initialize error list
            this.Errors = new List<KeyValuePair<string, string>>();
        }

        private void AddPageMetadata(string folderName, XmlElement parentElement) {
            // Create item node
            folderName = folderName.TrimEnd('\\');
            var pathId = folderName.Substring(this.BasePath.Length).Replace('\\', '/') + "/";
            var itemElement = this.CreateElement("item");
            itemElement.SetAttribute("path", pathId);

            // Import metadata from index page
            var mdFileName = Path.Combine(folderName, "index.md");
            if (File.Exists(mdFileName)) {
                foreach (var item in this.GetMetadataFromFile(mdFileName)) {
                    var separatorIndex = item.Key.IndexOf(':');
                    if (separatorIndex == -1 || separatorIndex == 0 || separatorIndex == item.Key.Length - 1) {
                        this.Errors.Add(new KeyValuePair<string, string>(mdFileName, $"Invalid syntax of metadata key \"{item.Key}\"."));
                    }
                    else if (string.IsNullOrEmpty(_nsmgr.LookupNamespace(item.Key.Substring(0, separatorIndex)))) {
                        this.Errors.Add(new KeyValuePair<string, string>(mdFileName, $"Unknown prefix of metadata key \"{item.Key}\"."));
                    }
                    else {
                        itemElement.AppendChild(this.CreateQualifiedElement(item.Key, item.Value));
                    }
                }
            }

            // Add item node to document
            parentElement.AppendChild(itemElement);

            // Recurse
            foreach (var item in Directory.GetDirectories(folderName)) {
                AddPageMetadata(item, itemElement);
            }

        }

        // Helper methods
        private IEnumerable<KeyValuePair<string, string>> GetMetadataFromFile(string fileName) {
            var metadataRead = false;
            using (var sr = File.OpenText(fileName)) {
                while (!sr.EndOfStream) {
                    var line = sr.ReadLine().Trim();
                    if (metadataRead && line == string.Empty) break;
                    if (line.Length < 7) continue;
                    if (!line.StartsWith("<!--", StringComparison.Ordinal) || !line.EndsWith("-->", StringComparison.Ordinal)) continue;
                    line = line.Substring(4, line.Length - 7).Trim(); // remove comment marks and trim whitespace
                    var lineData = line.Split('=', 2);
                    if (lineData.Length != 2) break; // not "name = value" comment
                    yield return new KeyValuePair<string, string>(lineData[0].Trim(), lineData[1].Trim());
                    metadataRead = true;
                }
            }
        }

        private XmlElement CreateQualifiedElement(string qualifiedName) {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));
            if (string.IsNullOrWhiteSpace(qualifiedName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(qualifiedName));

            var qnData = qualifiedName.Split(':');
            if (qnData.Length != 2) throw new ArgumentException($"Value '{qualifiedName}' must contain exactly one ':'.", nameof(qualifiedName));

            return this.CreateElement(qualifiedName, _nsmgr.LookupNamespace(qnData[0]));
        }

        private XmlElement CreateQualifiedElement(string qualifiedName, string text) {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));
            if (string.IsNullOrWhiteSpace(qualifiedName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(qualifiedName));

            var e = this.CreateQualifiedElement(qualifiedName);
            e.InnerText = text;
            return e;
        }

        private XmlElement CreateQualifiedElement(string qualifiedName, DateTime date) {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));
            if (string.IsNullOrWhiteSpace(qualifiedName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(qualifiedName));

            var e = this.CreateQualifiedElement(qualifiedName);
            e.InnerText = XmlConvert.ToString(date, XmlDateTimeSerializationMode.Local);
            return e;
        }

    }
}
