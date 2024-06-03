using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AzureStorageTools
{
    /// <summary>
    ///
    /// </summary>
    public class BlobHelper
    {
        /// <summary>
        ///
        /// </summary>
        private StorageSharedKeyCredential _AccountCredentials { get; set; }

        /// <summary>
        ///
        /// </summary>
        private BlobServiceClient _ServiceClient { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="blobServiceEndpoint"></param>
        /// <param name="storageAccountName"></param>
        /// <param name="storageAccountKey"></param>
        public BlobHelper(string blobServiceEndpoint, string storageAccountName, string storageAccountKey)
        {
            _AccountCredentials = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
            _ServiceClient = new BlobServiceClient(new Uri(blobServiceEndpoint), _AccountCredentials);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public AccountInfo GetAccountInfo()
        {
            var accInfo = _ServiceClient.GetAccountInfo();
            return accInfo;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <returns></returns>
        public BlobContainerClient GetOrCreateContainer(string containerName)
        {
            BlobContainerClient containerClient = _ServiceClient.GetBlobContainerClient(containerName);
            var result = containerClient.CreateIfNotExists();
            return containerClient;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <returns></returns>
        public bool DeleteContainer(string containerName)
        {
            BlobContainerClient containerClient = _ServiceClient.GetBlobContainerClient(containerName);
            if (containerClient.Exists())
            {
                var result = containerClient.Delete();
            }
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <returns></returns>
        public List<BlobItem> GetAllBlobs(string containerName)
        {
            var containerClient = GetOrCreateContainer(containerName);
            var blobs = containerClient.GetBlobs();
            var result = new List<BlobItem>();
            foreach (var blob in blobs)
            {
                result.Add(blob);
            }
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="blobName"></param>
        /// <param name="contents"></param>
        /// <returns></returns>
        public bool UploadBlob(string containerName, string blobName, string contents)
        {
            var containerClient = GetOrCreateContainer(containerName);
            // Get a reference to a blob
            var blobClient = containerClient.GetBlobClient(blobName);
            // Upload data from the local file
            var result = blobClient.Upload(ConvertToStream(contents), true);
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public string DownloadBlob(string containerName, string blobName)
        {
            var result = string.Empty;
            var containerClient = GetOrCreateContainer(containerName);
            if (containerClient.Exists())
            {
                // Get a reference to a blob
                var blobClient = containerClient.GetBlobClient(blobName);
                if (blobClient.Exists())
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        blobClient.DownloadTo(ms);
                        result = ConvertToString(ms);
                    }
                }
            }
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public bool ExistsBlob(string containerName, string blobName)
        {
            var containerClient = GetOrCreateContainer(containerName);
            // Get a reference to a blob
            var blobClient = containerClient.GetBlobClient(blobName);
            var result = blobClient.Exists();
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public bool DeleteBlob(string containerName, string blobName)
        {
            var containerClient = GetOrCreateContainer(containerName);
            // Get a reference to a blob
            var blobClient = containerClient.GetBlobClient(blobName);
            if (blobClient.Exists())
            {
                var result = blobClient.Delete();
            }
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        private static MemoryStream ConvertToStream(string contents)
        {
            // convert string to stream
            byte[] byteArray = Encoding.UTF8.GetBytes(contents);
            MemoryStream ms = new MemoryStream(byteArray);
            return ms;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private static string ConvertToString(MemoryStream stream)
        {
            var byteArray = stream.ToArray();
            var contents = ConvertToString(byteArray);
            return contents;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="byteArray"></param>
        /// <returns></returns>
        private static string ConvertToString(byte[] byteArray)
        {
            string result = Encoding.UTF8.GetString(byteArray);
            return result;
        }
    }
}