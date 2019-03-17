using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Altairis.Xml4web.AzureSync {
    class Program {
        public const int ERRORLEVEL_SUCCESS = 0;
        public const int ERRORLEVEL_FAILURE = 1;
        private const string WEB_CONTAINER_NAME = "$web";
        private const string SYS_CONTAINER_NAME = "xml4web";
        private const string STORAGE_INDEX_NAME = "storage-index.json";
        private const int MEGABYTE = 1048576;
        private const int HASH_BUFFER = 65536;
        private const int MAX_LISTING_ITEMS = 5000;
        private const int INDICATOR_STEP = 50;

        private static JobConfiguration config;
        private static List<JobOperation> operations = new List<JobOperation>();
        private static CloudBlobContainer webContainer;
        private static StorageCredentials credentials;
        private static CloudBlockBlob storageIndexBlob;
        private static StorageIndex storageIndex;


        static void Main(string[] args) {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine();

            Console.WriteLine($@"o   o o   o o    o  o                o     XML4web Azure Site Sync Tool");
            Console.WriteLine($@" \ /  |\ /| |    |  |                |     Version {version}");
            Console.WriteLine($@"  O   | O | |    o--O o   o   o o-o  O-o   Copyright (c) 2018");
            Console.WriteLine($@" / \  |   | |       |  \ / \ /  |-'  |  |  Michal A. Valášek - Altairis");
            Console.WriteLine($@"o   o o   o O---o   o   o   o   o-o  o-o   www.xml4web.com | www.rider.cz");
            Console.WriteLine();

            // Preparations
            LoadConfiguration(args);
            InitializeStorage();

            // Index everything and create list of operations
            IndexLocalFolder();
            IndexAzureStorage();
            AddNewLocalItems();

            // Perform operations
            DisplayStatistics();
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var runner = new JobRunner(credentials, config.ContentTypeMap, config.CacheControlRules);
            var result = runner.Run(operations);
            sw.Stop();

            // Save storage index
            SaveIndexFile();

            // Display results
            Console.WriteLine();
            if (result.Failed == 0) {
                Console.WriteLine($"All {result.Success} operations completed successfully in {sw.Elapsed}.");
            }
            else {
                Console.WriteLine($"Successfully completed {result.Success} operations, {result.Failed} failed in {sw.Elapsed}.");
                Console.WriteLine("See above messages on details about failed operations.");
            }
        }

        private static void SaveIndexFile() {
            Console.Write("Saving index...");
            var indexItems = from o in operations
                             where o.OperationType != JobOperationType.Delete && o.OperationType != JobOperationType.Undefined
                             select new KeyValuePair<string, string>(o.StorageUri.AbsolutePath.Remove(0, WEB_CONTAINER_NAME.Length + 2), o.ContentHash);
            storageIndex = new StorageIndex(indexItems);
            storageIndexBlob.Properties.ContentType = "application/json";
            storageIndex.Save(storageIndexBlob);
            Console.WriteLine("OK");
        }

        private static void InitializeStorage() {
            try {
                // Get storage account
                Console.Write("Initializing Azure Storage...");
                var result = CloudStorageAccount.TryParse(config.StorageConnectionString, out var account);
                if (!result) {
                    Console.WriteLine("Failed!");
                    Console.WriteLine("Cannot parse connection string.");
                    Environment.Exit(ERRORLEVEL_FAILURE);
                }
                credentials = account.Credentials;
                var client = account.CreateCloudBlobClient();
                Console.WriteLine("OK");

                // Get web container
                Console.Write("Getting web container...");
                webContainer = client.GetContainerReference(WEB_CONTAINER_NAME);
                var containerExists = webContainer.ExistsAsync().Result;
                if (!containerExists) {
                    Console.WriteLine("Failed!");
                    Console.WriteLine($"The {WEB_CONTAINER_NAME} container was not found.");
                    Environment.Exit(ERRORLEVEL_FAILURE);
                }
                Console.WriteLine($"OK, {webContainer.Uri}");

                // Get index container
                Console.Write("Getting index container...");
                var indexContainer = client.GetContainerReference(SYS_CONTAINER_NAME);
                indexContainer.CreateIfNotExistsAsync().Wait();
                Console.WriteLine($"OK, {indexContainer.Uri}");

                // Get storage index file
                Console.Write("Loading storage index...");
                storageIndexBlob = indexContainer.GetBlockBlobReference(STORAGE_INDEX_NAME);
                storageIndex = StorageIndex.LoadOrCreateEmpty(storageIndexBlob);
                Console.WriteLine($"OK, {storageIndex.Count} items");

            }
            catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static void LoadConfiguration(string[] args) {
            // Validate/load arguments
            if (args.Length != 2) {
                Console.WriteLine("USAGE: x4w-azsync jobname.json");
                Environment.Exit(ERRORLEVEL_SUCCESS);
            }

            var jobConfigFileName = args[1];
            if (!File.Exists(jobConfigFileName)) {
                Console.WriteLine($"ERROR: File '{jobConfigFileName}' was not found!");
                Environment.Exit(ERRORLEVEL_FAILURE);
            }

            // Load configuration
            Console.Write("Loading configuration...");
            try {
                config = JobConfiguration.Load(jobConfigFileName);
                Console.WriteLine("OK");
            }
            catch (Exception ex) {
                Console.WriteLine("Failed!");
                Console.WriteLine(ex.Message);
                Environment.Exit(ERRORLEVEL_FAILURE);
            }
        }

        private static void IndexLocalFolder() {
            Console.Write("Indexing local files...");
            var di = new DirectoryInfo(config.FolderName);
            var allFiles = di.GetFiles("*.*", SearchOption.AllDirectories);
            var newOps = allFiles.Select(fi => GetJobOperationFromFile(fi, di));
            operations.AddRange(newOps);
            var totalCount = allFiles.Length;
            var totalSize = newOps.Sum(x => x.Size) / MEGABYTE;
            Console.WriteLine($"OK, {totalCount} files, {totalSize:N0} MB");
        }

        private static JobOperation GetJobOperationFromFile(FileInfo fi, DirectoryInfo rootInfo) {
            // Get basic information
            var r = new JobOperation {
                LocalFileName = fi.FullName,
                LogicalName = fi.FullName.Remove(0, rootInfo.FullName.Length).Replace('\\', '/').Trim('/'),
                OperationType = JobOperationType.Undefined,
                Size = fi.Length,
            };

            // Compute storage URI
            if (!fi.Name.Equals(config.IndexFileName, StringComparison.OrdinalIgnoreCase) && config.RemoveExtensions.Contains(fi.Extension, StringComparer.OrdinalIgnoreCase)) {
                // Remove extension
                r.StorageUri = new Uri(webContainer.Uri + "/" + r.LogicalName.Substring(0, r.LogicalName.Length - fi.Extension.Length));
            }
            else {
                // Leave as is
                r.StorageUri = new Uri(webContainer.Uri + "/" + r.LogicalName);
            }

            return r;
        }

        private static void IndexAzureStorage() {
            Console.Write("Comparing with Azure Storage...");
            foreach (var item in storageIndex) {
                var storageUri = new Uri(webContainer.Uri + "/" + item.Key);

                // Get corresponding local file
                var op = operations.FirstOrDefault(x => x.StorageUri.Equals(storageUri));

                // None, delete extra file in storage
                if (op == null) {
                    operations.Add(new JobOperation {
                        OperationType = JobOperationType.Delete,
                        StorageUri = storageUri
                    });
                }
                else {
                    // Compute SHA256 hash of local file
                    op.ContentHash = GetFileHash(op.LocalFileName);

                    // Do nothing if hashes match
                    if (op.ContentHash.Equals(item.Value, StringComparison.OrdinalIgnoreCase)) {
                        op.OperationType = JobOperationType.Ignore;
                    }
                    else {
                        // Update if they don't
                        op.OperationType = JobOperationType.Update;
                    }
                }
            }
            Console.WriteLine("OK");
        }

        private static void AddNewLocalItems() {
            var pendingOps = operations.Where(x => x.OperationType == JobOperationType.Undefined);
            if (!pendingOps.Any()) return;

            Console.Write($"Processing remaining {pendingOps.Count()} local items:");
            var i = 0;

            foreach (var op in pendingOps) {
                op.OperationType = JobOperationType.Upload;
                op.ContentHash = GetFileHash(op.LocalFileName);
                i++;
                if (i % INDICATOR_STEP == 0) Console.Write(".");
            }

            Console.WriteLine("OK");
        }

        private static string GetFileHash(string fileName) {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(fileName));

            using (var ms = File.OpenRead(fileName))
#if NET47
            using (var sha = new System.Security.Cryptography.SHA256CryptoServiceProvider()) {
#else
            using (var sha = System.Security.Cryptography.SHA256.Create()) {
#endif
                // Compute MD5 hash
                var buffer = new byte[HASH_BUFFER];
                while (true) {
                    var bytesRead = ms.Read(buffer, 0, buffer.Length);
                    if (bytesRead == buffer.Length) {
                        sha.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
                    }
                    else {
                        sha.TransformFinalBlock(buffer, 0, bytesRead);
                        break;
                    }
                }

                // Convert to string
                var hashString = string.Join(string.Empty, sha.Hash.Select(x => x.ToString("X2")));
                return hashString;
            }
        }

        private static void DisplayStatistics() {
            Console.WriteLine();
            Console.WriteLine("Operation       Items  Size (MB)");
            Console.WriteLine("---------- ---------- ----------");
            Console.WriteLine("Delete     {0,10:N2} {1,10:N0}",
                operations.Count(x => x.OperationType == JobOperationType.Delete).ToString(),
                operations.Where(x => x.OperationType == JobOperationType.Delete).Sum(x => x.Size) / MEGABYTE);
            Console.WriteLine("Update     {0,10:N2} {1,10:N0}",
                operations.Count(x => x.OperationType == JobOperationType.Update).ToString(),
                operations.Where(x => x.OperationType == JobOperationType.Update).Sum(x => x.Size) / MEGABYTE);
            Console.WriteLine("Upload     {0,10:N2} {1,10:N0}",
                operations.Count(x => x.OperationType == JobOperationType.Upload).ToString(),
                operations.Where(x => x.OperationType == JobOperationType.Upload).Sum(x => x.Size) / MEGABYTE);
            Console.WriteLine("Ignore     {0,10:N2} {1,10:N0}",
                operations.Count(x => x.OperationType == JobOperationType.Ignore).ToString(),
                operations.Where(x => x.OperationType == JobOperationType.Ignore).Sum(x => x.Size) / MEGABYTE);
            Console.WriteLine();
        }

    }
}
