using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Newtonsoft.Json;

namespace Altairis.Xml4web.AzureSync {
    public class JobConfiguration {

        public static JobConfiguration Load(string fileName) {
            Contract.Ensures(Contract.Result<JobConfiguration>() != null);
            Contract.Requires(string.IsNullOrEmpty(fileName) == false);

            var json = File.ReadAllText(fileName);
            var obj = JsonConvert.DeserializeObject<JobConfiguration>(json);
            return obj;
        }

        public string StorageConnectionString { get; set; }
        public string FolderName { get; set; }
        public bool ConvertToLowercase { get; set; }
        public string IndexFileName { get; set; }
        public IEnumerable<string> RemoveExtensions { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> ContentTypeMap { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> CacheControlRules { get; set; } = new Dictionary<string, string>();

    }
}
