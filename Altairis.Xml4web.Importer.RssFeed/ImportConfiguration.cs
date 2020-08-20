using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Newtonsoft.Json;

namespace Altairis.Xml4web.Importer.RssFeed {
    internal class ImportConfiguration {

        public static ImportConfiguration Load(string fileName) {
            Contract.Requires(string.IsNullOrEmpty(fileName) == false);

            var json = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<ImportConfiguration>(json);
        }

        public string RssUrl { get; set; }

        public string FolderName { get; set; }

        public string ItemXPath { get; set; }

        public Dictionary<string, string> ImportMetadataFromXPath { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, string> StaticMetadata { get; set; } = new Dictionary<string, string>();

        public string DateXPath { get; set; }

        public string DateLocale { get; set; }

        public string FileName { get; set; }

    }
}
