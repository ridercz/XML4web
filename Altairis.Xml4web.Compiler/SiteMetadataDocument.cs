using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Altairis.Xml4web.Compiler {
    public class SiteMetadataDocument : XmlDocument {
        private readonly XmlNamespaceManager nsmgr;

        public ICollection<KeyValuePair<string, string>> Errors { get; }

        public string SourceFolderName { get; private set; }

        public static SiteMetadataDocument CreateFromFolder(string sourceFolderName) {
            var doc = new SiteMetadataDocument(Path.Combine(sourceFolderName, "namespaces.txt")) {
                SourceFolderName = sourceFolderName.TrimEnd('\\')
            };
            doc.ScanFolder(doc.SourceFolderName, doc.DocumentElement);
            return doc;
        }

        private SiteMetadataDocument(string namespaceFile) {
            if (namespaceFile == null) throw new ArgumentNullException(nameof(namespaceFile));
            if (string.IsNullOrWhiteSpace(namespaceFile)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(namespaceFile));

            // Create root element
            this.AppendChild(this.CreateElement("siteMetadata"));

            // Add default namespaces
            this.nsmgr = new XmlNamespaceManager(this.NameTable);
            this.nsmgr.AddNamespace("dcterms", Namespaces.DCTerms);
            this.nsmgr.AddNamespace("dc", Namespaces.DC);
            this.nsmgr.AddNamespace("x4w", Namespaces.X4W);
            this.nsmgr.AddNamespace("x4h", Namespaces.X4H);
            this.nsmgr.AddNamespace("x4f", Namespaces.X4F);

            // Add namespaces from file
            var lines = File.ReadAllLines(namespaceFile);
            foreach (var line in lines) {
                var data = line.Split(new char[] { ':' }, 2);
                if (data.Length != 2) continue;
                this.nsmgr.AddNamespace(data[0], data[1]);
                this.DocumentElement.SetAttribute("xmlns:" + data[0], data[1]);
            }

            // Add creation date
            this.DocumentElement.AppendChild(this.CreateQualifiedElement("dcterms:created", DateTime.Now));

            // Initialize error list
            this.Errors = new List<KeyValuePair<string, string>>();
        }

        private void ScanFolder(string folderName, XmlElement parentElement) {
            // Create item node
            folderName = folderName.TrimEnd('\\');
            var pathId = folderName.Substring(this.SourceFolderName.Length).Replace('\\', '/');
            var folderElement = this.CreateElement("folder");
            folderElement.SetAttribute("path", string.IsNullOrEmpty(pathId) ? "/" : pathId);

            // Import metadata from index page
            var indexFileName = Path.Combine(folderName, "index.md");
            if (File.Exists(indexFileName)) {
                foreach (var item in this.GetMetadataElementsForPage(indexFileName)) {
                    folderElement.AppendChild(item);
                }
            }

            // Import metadata from other pages
            foreach (var fileName in Directory.GetFiles(folderName, "*.md")) {
                var fileElement = this.CreateElement("page");
                fileElement.SetAttribute("path", pathId + "/" + Path.GetFileNameWithoutExtension(fileName));
                foreach (var item in this.GetMetadataElementsForPage(fileName)) {
                    fileElement.AppendChild(item);
                }
                folderElement.AppendChild(fileElement);
            }

            // Add item node to document
            parentElement.AppendChild(folderElement);

            // Recurse folders
            foreach (var item in Directory.GetDirectories(folderName)) {
                this.ScanFolder(item, folderElement);
            }

        }

        private IEnumerable<XmlElement> GetMetadataElementsForPage(string mdFileName) {
            var fi = new FileInfo(mdFileName);
            if (!fi.Exists) yield break;

            // Add general file metadata
            yield return this.CreateQualifiedElement("x4f:creationTime", fi.CreationTime);
            yield return this.CreateQualifiedElement("x4f:lastAccessTime", fi.LastAccessTime);
            yield return this.CreateQualifiedElement("x4f:lastWriteTime", fi.LastWriteTime);
            yield return this.CreateQualifiedElement("x4f:size", fi.Length);

            // Add markdown-specific metadata
            if (Path.GetExtension(mdFileName).Equals(".md", StringComparison.OrdinalIgnoreCase)) {
                foreach (var item in this.GetMetadataFromMarkdownFile(mdFileName)) {
                    var separatorIndex = item.Key.IndexOf(':');
                    if (separatorIndex == -1 || separatorIndex == 0 || separatorIndex == item.Key.Length - 1) {
                        this.Errors.Add(new KeyValuePair<string, string>(mdFileName, $"Invalid syntax of metadata key \"{item.Key}\"."));
                    } else if (string.IsNullOrEmpty(this.nsmgr.LookupNamespace(item.Key.Substring(0, separatorIndex)))) {
                        this.Errors.Add(new KeyValuePair<string, string>(mdFileName, $"Unknown prefix of metadata key \"{item.Key}\"."));
                    } else {
                        yield return this.CreateQualifiedElement(item.Key, item.Value);
                    }
                }
            }
        }

        // Helper methods

        private IEnumerable<KeyValuePair<string, string>> GetMetadataFromMarkdownFile(string mdFileName) {
            var metadataRead = false;
            using (var sr = File.OpenText(mdFileName)) {
                while (!sr.EndOfStream) {
                    var line = sr.ReadLine().Trim();
                    if (metadataRead && line == string.Empty) break;
                    if (line.Length < 7) continue;
                    if (!line.StartsWith("<!--", StringComparison.Ordinal) || !line.EndsWith("-->", StringComparison.Ordinal)) continue;
                    line = line.Substring(4, line.Length - 7).Trim(); // remove comment marks and trim whitespace
                    var lineData = line.Split(new char[] { '=' }, 2);
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

            return this.CreateElement(qualifiedName, this.nsmgr.LookupNamespace(qnData[0]));
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

        private XmlElement CreateQualifiedElement(string qualifiedName, long number) {
            if (qualifiedName == null) throw new ArgumentNullException(nameof(qualifiedName));
            if (string.IsNullOrWhiteSpace(qualifiedName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(qualifiedName));

            var e = this.CreateQualifiedElement(qualifiedName);
            e.InnerText = XmlConvert.ToString(number);
            return e;
        }

    }
}
