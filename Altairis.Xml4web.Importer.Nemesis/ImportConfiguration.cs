﻿using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Newtonsoft.Json;

namespace Altairis.Xml4web.Importer.Nemesis {
    internal class ImportConfiguration {

        public static ImportConfiguration Load(string fileName) {
            Contract.Requires(string.IsNullOrEmpty(fileName) == false);

            var json = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<ImportConfiguration>(json);
        }

        public string ConnectionString { get; set; }

        public string FolderName { get; set; }

        public bool ImportPictures { get; set; }

        public string ImportPicturesPath { get; set; }

        public string ImportPicturesUrl { get; set; }

        public bool ImportExternal { get; set; }

        public bool ImportPublished { get; set; }

        public bool ImportUnpublished { get; set; }

        public string FileNameFormat { get; set; }

        public string IdentifierFormat { get; set; }

        public bool ConvertHtmlToMarkdown { get; set; }

        public string IdMapFileName { get; set; }

        public string LinkListFileName { get; set; }

        public Dictionary<string, string> SiteMetadata { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, string> LinkReplacements { get; set; } = new Dictionary<string, string>();

    }
}
