﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Altairis.Xml4web.AzureSync {

    public class StorageIndex : Dictionary<string, string> {

        public StorageIndex() { }

#if NET47
        public StorageIndex(IEnumerable<KeyValuePair<string, string>> collection) {
            foreach (var item in collection) {
                base.Add(item.Key, item.Value);
            }
        }
#else
        public StorageIndex(IEnumerable<KeyValuePair<string, string>> collection) : base(collection) { }
#endif
        public static async Task<StorageIndex> LoadOrCreateEmptyAsync(CloudBlob blob) {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            if (!await blob.ExistsAsync()) return new StorageIndex();

            // Download blob to temporary file
            var fileName = Path.GetTempFileName();
            await blob.DownloadToFileAsync(fileName, FileMode.Create);

            // Read the file
            var json = File.ReadAllText(fileName);
            File.Delete(fileName);

            // Return deserialized array
            return JsonConvert.DeserializeObject<StorageIndex>(json);
        }

        public static StorageIndex LoadOrCreateEmpty(CloudBlob blob) => LoadOrCreateEmptyAsync(blob).Result;

        public async Task SaveAsync(CloudBlockBlob blob) {
            if (blob == null) throw new ArgumentNullException(nameof(blob));

            // Serialize to temporary file
            var json = JsonConvert.SerializeObject(this);
            var fileName = Path.GetTempFileName();
            File.WriteAllText(fileName, json);

            // Upload file to blob
            await blob.SmartUploadFileAsync(fileName);
            File.Delete(fileName);

        }

        public void Save(CloudBlockBlob blob) => this.SaveAsync(blob).Wait();
    }
}
