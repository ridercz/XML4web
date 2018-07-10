using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Altairis.Xml4web.Importer.Nemesis {
    public static class Extensions {

        public static string ToSingleLine(this string s) {
            if (string.IsNullOrEmpty(s)) return s;
            s = Regex.Replace(s, @"\s", " ");
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            s = s.Trim();
            return s;
        }

        public static string ToUrlKey(this string s) {
            if (string.IsNullOrEmpty(s)) return "null";
            s = s.ToSingleLine().RemoveDiacritics().ToLower();
            s = Regex.Replace(s, "[^a-z0-9]", "-");
            s = s.Trim('-');
            while (s.Contains("--")) s = s.Replace("--", "-");
            if (string.IsNullOrWhiteSpace(s)) s = "null";
            return s;
        }

        public static string RemoveDiacritics(this string s) {
            s = s.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < s.Length; i++) {
                if (CharUnicodeInfo.GetUnicodeCategory(s[i]) != UnicodeCategory.NonSpacingMark) sb.Append(s[i]);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static void AppendMetadataLine(this StringBuilder sb, string name, object value) {
            if (value == null || value is DBNull || string.IsNullOrWhiteSpace(value.ToString())) return;
            sb.AppendLine($"<!-- {name} = {value.ToString().ToSingleLine()} -->");
        }

    }
}
