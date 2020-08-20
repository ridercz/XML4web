using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using Markdig;

namespace Altairis.Xml4web.Compiler {
    public class XsltHelper {
        private readonly BuildConfiguration _config;

        public XsltHelper(BuildConfiguration config) {
            this._config = config;
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

        public string CurrentDateTime() => XmlConvert.ToString(DateTime.Now, XmlDateTimeSerializationMode.RoundtripKind);

        public string ComputeHash(string path) {
            var fullFileName = Path.Combine(this._config.StaticFolder, path.Trim('/', '\\'));
            using (var sha = new System.Security.Cryptography.SHA1Managed()) {
                var hash = sha.ComputeHash(File.ReadAllBytes(fullFileName));
                return string.Join(string.Empty, hash.Select(x => x.ToString("X2")));
            }
        }

        public string GetItemHtml(string path) {
            var fullFileName = Path.Combine(this._config.SourceFolder, path.Trim('/', '\\') + ".md");
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

        public string UrlKey(string s) {
            if (string.IsNullOrEmpty(s)) return "null";
            s = this.RemoveDiacritics(s).ToLower();
            s = Regex.Replace(s, "[^a-z0-9]", "-");
            s = s.Trim('-');
            while (s.Contains("--")) s = s.Replace("--", "-");
            if (string.IsNullOrWhiteSpace(s)) s = "null";
            return s;
        }

        public string RemoveDiacritics(string s) {
            s = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            for (var i = 0; i < s.Length; i++) {
                if (CharUnicodeInfo.GetUnicodeCategory(s[i]) != UnicodeCategory.NonSpacingMark) sb.Append(s[i]);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public string UrlEncode(string s) => HttpUtility.UrlEncode(s);

        public string Replace(string s, string oldValue, string newValue) => s?.Replace(oldValue, newValue);

    }
}
