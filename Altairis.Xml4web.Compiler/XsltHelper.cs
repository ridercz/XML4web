using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Markdig;

namespace Altairis.Xml4web.Compiler {
    public class XsltHelper {
        private readonly BuildConfiguration _config;

        public XsltHelper(BuildConfiguration config) {
            _config = config;
        }

        public string FormatDateTime(string dateTime, string formatString, string culture) {
            if (dateTime == null) throw new ArgumentNullException(nameof(dateTime));
            if (string.IsNullOrEmpty(dateTime)) throw new ArgumentException("Value cannot be null or empty string.", nameof(dateTime));
            if (formatString == null) throw new ArgumentNullException(nameof(formatString));
            if (string.IsNullOrEmpty(formatString)) throw new ArgumentException("Value cannot be null or empty string.", nameof(formatString));
            if (culture == null) throw new ArgumentNullException(nameof(culture));
            if (string.IsNullOrEmpty(culture)) throw new ArgumentException("Value cannot be null or empty string.", nameof(culture));

            var dt = XmlConvert.ToDateTime(dateTime, XmlDateTimeSerializationMode.RoundtripKind);
            var ci = new CultureInfo(culture);
            return dt.ToString(formatString, ci);
        }

        public string ImportText(string fileName) {
            var fullFileName = Path.Combine(_config.SourceFolder, fileName.Trim('/', '\\'));
            return File.ReadAllText(fullFileName);
        }

        public string ImportMarkdown(string fileName) {
            var fullFileName = Path.Combine(_config.SourceFolder, fileName.Trim('/', '\\'));
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var mdLines = File.ReadAllLines(fullFileName);
            var mdSb = new StringBuilder();
            foreach (var line in mdLines) {
                var s = line.Trim();
                if (s.StartsWith("<!--", StringComparison.Ordinal) && s.EndsWith("-->", StringComparison.Ordinal)) continue;
                mdSb.AppendLine(line);
            }
            var html = Markdown.ToHtml(mdSb.ToString(), pipeline);
            return html;
        }

        public string CopyAttachments(string sourceFolder, string targetFolder) {
            var fullSourceFolder = Path.Combine(_config.SourceFolder, sourceFolder.Trim('/', '\\'));
            var fullTargetFolder = Path.Combine(_config.TargetFolder, targetFolder.Trim('/', '\\'));

            foreach (var file in Directory.GetFiles(fullSourceFolder)) {
                var fileNameOnly = Path.GetFileName(file);
                if (fileNameOnly.Equals("index.md", StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(fullTargetFolder);
                File.Copy(file, Path.Combine(fullTargetFolder, fileNameOnly), overwrite: true);
            }
            return string.Empty;
        }

    }
}
