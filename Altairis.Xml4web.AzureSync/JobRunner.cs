﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Altairis.Xml4web.AzureSync {
    public class JobRunner {
        private const int MEGABYTE = 1048576;                   // 1 MB
        private const int FILE_SIZE_THRESHOLD = 32 * MEGABYTE;  // 32 MB
        private const int BLOCK_SIZE = 4 * MEGABYTE;            // 4 MB

        public StorageCredentials StorageCredentials { get; }
        public Dictionary<string, string> ContentTypeMap { get; }
        public int RetryCount { get; }
        public int RetryWaitMiliseconds { get; }

        public JobRunner(StorageCredentials storageCredentials, Dictionary<string, string> contentTypeMap, int retryCount = 3, int retryWaitMiliseconds = 500) {
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
            if (retryWaitMiliseconds < 0) throw new ArgumentOutOfRangeException(nameof(retryWaitMiliseconds));

            this.StorageCredentials = storageCredentials ?? throw new ArgumentNullException(nameof(storageCredentials));
            this.ContentTypeMap = contentTypeMap ?? throw new ArgumentNullException(nameof(contentTypeMap));
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
                }
                else {
                    fc++;
                }
            }
            return (sc, fc);
        }

        public bool Run(JobOperation job) {
            switch (job.OperationType) {
                case JobOperationType.Upload:
                    return this.RunWithRetry(() => this.RunUploadJob(job));
                case JobOperationType.Update:
                    if (!this.RunWithRetry(() => this.RunDeleteJob(job))) return false;
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
            blob.Metadata.Add(Program.HASH_HEADER_NAME, job.ContentHash);
            blob.Properties.ContentType = this.ContentTypeMap.FirstOrDefault(x => x.Key.Equals(Path.GetExtension(job.LocalFileName), StringComparison.OrdinalIgnoreCase)).Value ?? "application/octet-stream";
            blob.SmartUploadFile(job.LocalFileName, (number, count) => { Console.Write("."); });
            Console.WriteLine("OK");
        }

        private bool RunWithRetry(Action action) {
            var remainingRetryCount = this.RetryCount;

            while (true) {
                try {
                    action();
                    return true;
                }
                catch (Exception ex) {
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
