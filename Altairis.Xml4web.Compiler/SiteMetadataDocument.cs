using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using SixLabors.ImageSharp;

namespace Altairis.Xml4web.Compiler {
    public class SiteMetadataDocument : XmlDocument {
        private readonly XmlNamespaceManager nsmgr;

        // Factory method

        public static SiteMetadataDocument CreateFromFolder(string sourceFolderName) {
            var doc = new SiteMetadataDocument(Path.Combine(sourceFolderName, "_namespaces")) {
                SourceFolderName = sourceFolderName.TrimEnd('\\')
            };
            doc.ScanFolder(doc.SourceFolderName, doc.DocumentElement);
            return doc;
        }

        // Constructor

        private SiteMetadataDocument(string namespaceFile = null) {
            // Create root element
            this.AppendChild(this.CreateElement("siteMetadata"));

            // Add default namespaces
            this.nsmgr = new XmlNamespaceManager(this.NameTable);
            this.nsmgr.AddNamespace("dcterms", Namespaces.DCTerms);
            this.nsmgr.AddNamespace("dc", Namespaces.DC);
            this.nsmgr.AddNamespace("x4w", Namespaces.X4W);
            this.nsmgr.AddNamespace("x4f", Namespaces.X4F);
            this.nsmgr.AddNamespace("exif", Namespaces.Exif);

            // Add namespaces from file
            if (!string.IsNullOrEmpty(namespaceFile) && File.Exists(namespaceFile)) {
                var lines = File.ReadAllLines(namespaceFile).Select(s => s.Trim());
                foreach (var line in lines) {
                    if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
                    var data = line.Split(new char[] { ':' }, 2);
                    if (data.Length != 2) continue;
                    this.nsmgr.AddNamespace(data[0], data[1]);
                }
            }

            // Add namespaces
            foreach (var nsdef in this.nsmgr.GetNamespacesInScope(XmlNamespaceScope.All)) {
                this.DocumentElement.SetAttribute("xmlns:" + nsdef.Key, nsdef.Value);
            }


            // Add creation date
            this.DocumentElement.AppendChild(this.CreateQualifiedElement("dcterms:created", DateTime.Now));

            // Initialize error list
            this.Errors = new List<KeyValuePair<string, string>>();
        }

        // Properties

        public ICollection<KeyValuePair<string, string>> Errors { get; }

        public string SourceFolderName { get; private set; }

        // Methods

        private void ScanFolder(string folderName, XmlElement parentElement) {
            // Create folder node
            folderName = folderName.TrimEnd('\\');
            var pathId = folderName.Substring(this.SourceFolderName.Length).Replace('\\', '/');
            var folderElement = this.CreateElement("folder");

            // Add path and name
            if (!string.IsNullOrEmpty(pathId)) {
                folderElement.SetAttribute("path", pathId);
                folderElement.AppendChild(this.CreateQualifiedElement("x4f:name", pathId.Substring(pathId.LastIndexOf('/') + 1)));
            }

            // Import metadata from index page, if any
            var indexFileName = Path.Combine(folderName, "index.md");
            if (File.Exists(indexFileName)) {
                folderElement.AppendChildren(this.GetMetadataElementsForPage(indexFileName));
                folderElement.AppendChildren(this.GetMetadataElementsForFile(indexFileName));
            }

            // Process files
            foreach (var fileName in Directory.GetFiles(folderName)) {
                // Ignore system files
                if (Path.GetFileName(fileName).StartsWith("_") || Path.GetFileName(fileName).StartsWith(".") || Path.GetFileName(fileName).Contains("~")) continue;

                var extension = Path.GetExtension(fileName).ToLower();
                XmlElement itemElement;

                switch (extension) {
                    case ".md":
                        // Markdown file
                        var path = Path.GetFileName(fileName).EndsWith("index.md", StringComparison.OrdinalIgnoreCase)
                            ? pathId
                            : string.Concat(pathId, "/", Path.GetFileNameWithoutExtension(fileName));
                        if (string.IsNullOrEmpty(path)) path = "/";
                        itemElement = this.CreateElement("page");
                        itemElement.SetAttribute("path", path);
                        itemElement.SetAttribute("filePath", string.Concat(pathId, "/", Path.GetFileNameWithoutExtension(fileName)));
                        itemElement.AppendChildren(this.GetMetadataElementsForPage(fileName));
                        itemElement.AppendChild(this.CreateQualifiedElement("x4f:name", Path.GetFileName(fileName)));
                        break;
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                        // Image file
                        itemElement = this.CreateElement("image");
                        itemElement.SetAttribute("path", pathId + "/" + Path.GetFileName(fileName));
                        itemElement.AppendChildren(this.GetMetadataElementsForImage(fileName));
                        itemElement.AppendChild(this.CreateQualifiedElement("x4f:name", Path.GetFileName(fileName)));
                        break;
                    default:
                        // Other file
                        itemElement = this.CreateElement("file");
                        itemElement.SetAttribute("path", pathId + "/" + Path.GetFileName(fileName));
                        itemElement.AppendChild(this.CreateQualifiedElement("x4f:name", Path.GetFileName(fileName)));
                        break;
                }

                // Add general file metadata
                itemElement.AppendChildren(this.GetMetadataElementsForFile(fileName));
                folderElement.AppendChild(itemElement);
            }

            // Add folder node to document
            parentElement.AppendChild(folderElement);

            // Recurse folders
            foreach (var item in Directory.GetDirectories(folderName)) {
                this.ScanFolder(item, folderElement);
            }
        }

        private IEnumerable<XmlElement> GetMetadataElementsForFile(string fileName) {
            var fi = new FileInfo(fileName);
            if (!fi.Exists) return Enumerable.Empty<XmlElement>();

            // Add general file metadata
            return new XmlElement[] {
                this.CreateQualifiedElement("x4f:creationTime", fi.CreationTime),
                this.CreateQualifiedElement("x4f:lastAccessTime", fi.LastAccessTime),
                this.CreateQualifiedElement("x4f:lastWriteTime", fi.LastWriteTime),
                this.CreateQualifiedElement("x4f:size", fi.Length),
            };
        }

        private IEnumerable<XmlElement> GetMetadataElementsForImage(string imageFileName) {
            var fi = new FileInfo(imageFileName);
            if (!fi.Exists) return Enumerable.Empty<XmlElement>();

            IEnumerable<KeyValuePair<string, string>> exif;

            var cacheFileName = fi.FullName + "~exifcache";
            if (File.Exists(cacheFileName)) {
                // Use found cache
                var cacheLines = File.ReadAllLines(cacheFileName);
                exif = cacheLines.Select(x => {
                    var data = x.Split('\t');
                    return new KeyValuePair<string, string>(data[0], data[1]);
                });
            } else {
                // Cache not found - read data and create cache
                exif = this.ReadExifMetadata(fi.FullName);
                var cacheLines = exif.Select(x => string.Join("\t", x.Key, x.Value));
                File.WriteAllLines(cacheFileName, cacheLines);
            }

            return exif.Select(x => this.CreateQualifiedElement(x.Key, x.Value));
        }

        private IEnumerable<XmlElement> GetMetadataElementsForPage(string mdFileName) {
            var fi = new FileInfo(mdFileName);
            if (!fi.Exists) yield break;

            // Add markdown-specific metadata
            if (Path.GetExtension(mdFileName).Equals(".md", StringComparison.OrdinalIgnoreCase)) {
                foreach (var item in this.ReadMarkdownMetadata(mdFileName)) {
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

        // Metadata reading methods

        private IEnumerable<KeyValuePair<string, string>> ReadMarkdownMetadata(string mdFileName) {
            var metadataRead = false;
            using var sr = File.OpenText(mdFileName);
            while (!sr.EndOfStream) {
                var line = sr.ReadLine().Trim();
                if (metadataRead && string.IsNullOrEmpty(line)) break;
                if (line.Length < 7) continue;
                if (!line.StartsWith("<!--", StringComparison.Ordinal) || !line.EndsWith("-->", StringComparison.Ordinal)) continue;
                line = line[4..^3].Trim(); // remove comment marks and trim whitespace
                var lineData = line.Split(new char[] { '=' }, 2);
                if (lineData.Length != 2) break; // not "name = value" comment
                yield return new KeyValuePair<string, string>(lineData[0].Trim(), lineData[1].Trim());
                metadataRead = true;
            }
        }

        private IEnumerable<KeyValuePair<string, string>> ReadExifMetadata(string imageFileName) {
            using var img = Image.Load(imageFileName);
            if (img.Metadata.ExifProfile != null) {
                // Get EXIF metadata
                foreach (var item in img.Metadata.ExifProfile.Values) {
                    var stringValue = item.ToPrettyString();
                    if (string.IsNullOrWhiteSpace(stringValue)) continue;

                    var stringName = item.Tag.ToString();
                    stringName = char.ToLowerInvariant(stringName[0]) + stringName.Substring(1);

                    yield return new KeyValuePair<string, string>("exif:" + stringName, stringValue);
                }
            } else {
                // No EXIF metadata found, send at least size
                yield return new KeyValuePair<string, string>("exif:pixelXDimension", img.Width.ToString());
                yield return new KeyValuePair<string, string>("exif:pixelYDimension", img.Height.ToString());
            }
        }

        // XML helper methods

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
