using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Altairis.Xml4web.AzureSync {
    public static class BlobExtensions {
        private const int MEGABYTE = 1048576;                   // 1 MB
        private const int FILE_SIZE_THRESHOLD = 32 * MEGABYTE;  // 32 MB
        private const int BLOCK_SIZE = 4 * MEGABYTE;            // 4 MB

        public static void SmartUploadFile(this CloudBlockBlob blob, string fileName, Action<int, int> blobProgressCallback = null) {
            blob.SmartUploadFileAsync(fileName, blobProgressCallback).Wait();
        }

        public static async Task SmartUploadFileAsync(this CloudBlockBlob blob, string fileName, Action<int, int> blobProgressCallback = null) {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(fileName));

            var fileInfo = new FileInfo(fileName);

            using (var file = fileInfo.OpenRead()) {
                if (file.Length <= FILE_SIZE_THRESHOLD) {
                    await blob.UploadFromStreamAsync(file);
                }
                else {
                    var blockCount = (int)Math.Ceiling(((float)file.Length / BLOCK_SIZE));
                    var blockIds = new List<string>();
                    var currentBlockId = 0;
                    while (file.Position < file.Length) {
                        var bufferSize = BLOCK_SIZE < file.Length - file.Position ? BLOCK_SIZE : file.Length - file.Position;
                        var buffer = new byte[bufferSize];
                        file.Read(buffer, 0, buffer.Length);

                        using (var stream = new MemoryStream(buffer)) {
                            stream.Position = 0;
                            var blockIdString = Convert.ToBase64String(BitConverter.GetBytes(currentBlockId));
                            blob.PutBlockAsync(blockIdString, stream, null).Wait();
                            blockIds.Add(blockIdString);
                            currentBlockId++;
                            blobProgressCallback(currentBlockId, blockCount);
                        }
                    }
                    blob.PutBlockListAsync(blockIds).Wait();
                }
            }
        }

    }
}
