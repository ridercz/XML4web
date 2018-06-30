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
        private readonly string _basePath;

        public XsltHelper(string basePath) {
            _basePath = basePath;
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
            fileName = fileName.Trim('/', '\\'); var fullFileName = Path.Combine(_basePath, fileName);
            return File.ReadAllText(fullFileName);
        }

        public string ImportMarkdown(string fileName) {
            fileName = fileName.Trim('/', '\\');
            var fullFileName = Path.Combine(_basePath, fileName);
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


    }
}
