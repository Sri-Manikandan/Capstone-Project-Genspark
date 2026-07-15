using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EMSBLLLibrary.Services
{
    // Production image storage. Uploads to the public-read "event-images" container and returns
    // the blob's absolute URL. The container is created by Bicep with anonymous blob read; this
    // class never creates it (creating a public container from code may be denied by the account).
    public class AzureBlobImageStorage : IImageStorage
    {
        private readonly BlobContainerClient _container;

        public AzureBlobImageStorage(IConfiguration config)
        {
            var connectionString = config["Storage:ConnectionString"]
                ?? throw new InvalidOperationException(
                    "Storage:ConnectionString is not configured. Set Storage__ConnectionString.");
            var containerName = config["Storage:ContainerName"] ?? "event-images";
            _container = new BlobContainerClient(connectionString, containerName);
        }

        public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
        {
            var blob = _container.GetBlobClient(ImageContentType.NewFileName(contentType));
            await blob.UploadAsync(
                content,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
                ct);
            return blob.Uri.ToString();
        }
    }
}
