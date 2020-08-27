using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Altairis.Xml4web.AzureSync {
    public class JobRunner {
        private const string HASH_HEADER_NAME = "X4W_SHA256";

        public StorageCredentials StorageCredentials { get; }
        public Dictionary<string, string> ContentTypeMap { get; }
        public Dictionary<string, string> CacheControlRules { get; }
        public int RetryCount { get; }
        public int RetryWaitMiliseconds { get; }

        public JobRunner(StorageCredentials storageCredentials, Dictionary<string, string> contentTypeMap, Dictionary<string, string> cacheControlRules, int retryCount = 3, int retryWaitMiliseconds = 500) {
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
            if (retryWaitMiliseconds < 0) throw new ArgumentOutOfRangeException(nameof(retryWaitMiliseconds));

            this.StorageCredentials = storageCredentials ?? throw new ArgumentNullException(nameof(storageCredentials));
            this.ContentTypeMap = contentTypeMap ?? throw new ArgumentNullException(nameof(contentTypeMap));
            this.CacheControlRules = cacheControlRules ?? throw new ArgumentNullException(nameof(cacheControlRules));
            this.RetryCount = retryCount;
            this.RetryWaitMiliseconds = retryWaitMiliseconds;
        }

        public (int Success, int Failed) Run(IEnumerable<JobOperation> jobs) {
            var sc = 0;
            var fc = 0;
            foreach (var job in jobs) {
                var r = this.Run(job);
                if (r) {
                    sc++;
                } else {
                    fc++;
                }
            }
            return (sc, fc);
        }

        public bool Run(JobOperation job) {
            switch (job.OperationType) {
                case JobOperationType.Upload:
                case JobOperationType.Update:
                    return this.RunWithRetry(() => this.RunUploadJob(job));
                case JobOperationType.Delete:
                    return this.RunWithRetry(() => this.RunDeleteJob(job));
                case JobOperationType.Undefined:
                case JobOperationType.Ignore:
                default:
                    return true;
            }
        }

        private void RunDeleteJob(JobOperation job) {
            if (job == null) throw new ArgumentNullException(nameof(job));

            Console.Write($"Deleting {job.StorageUri.AbsolutePath}...");
            var blob = new CloudBlob(job.StorageUri, this.StorageCredentials);
            var result = blob.DeleteIfExistsAsync().Result;
            Console.WriteLine(result ? "OK" : "Not found");
        }

        private void RunUploadJob(JobOperation job) {
            if (job == null) throw new ArgumentNullException(nameof(job));
            Console.Write($"Uploading {job.LogicalName}: ");
            var blob = new CloudBlockBlob(job.StorageUri, this.StorageCredentials);
            blob.Metadata.Add(HASH_HEADER_NAME, job.ContentHash);
            blob.Properties.ContentType = this.ContentTypeMap.FirstOrDefault(x => x.Key.Equals(Path.GetExtension(job.LocalFileName), StringComparison.OrdinalIgnoreCase)).Value ?? "application/octet-stream";
            blob.Properties.CacheControl = this.CacheControlRules.FirstOrDefault(x => Regex.IsMatch(job.LogicalName, x.Key)).Value ?? "no-cache, no-store, must-revalidate";
            blob.SmartUploadFile(job.LocalFileName, (number, count) => Console.Write("."));
            Console.WriteLine("OK");
        }

        private bool RunWithRetry(Action action) {
            var remainingRetryCount = this.RetryCount;

            while (true) {
                try {
                    action();
                    return true;
                } catch (Exception ex) {
                    if (remainingRetryCount == 0) {
                        Console.WriteLine($"Failed!");
                        Console.WriteLine(ex.Message);
                        return false;
                    }
                    Console.WriteLine($"Failed ({ex.Message}), retrying...");
                    remainingRetryCount--;
                    System.Threading.Thread.Sleep(this.RetryWaitMiliseconds);
                }
            }
        }

    }
}
