namespace EMSBLLLibrary.Interfaces
{
    // Stores an uploaded image and returns its absolute, publicly reachable URL.
    // Implementations: LocalImageStorage (dev, filesystem) and AzureBlobImageStorage (prod).
    public interface IImageStorage
    {
        Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default);
    }
}
