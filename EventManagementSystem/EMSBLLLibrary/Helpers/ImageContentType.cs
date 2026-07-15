namespace EMSBLLLibrary.Helpers
{
    // Single source of truth for what an event image may be and how uploaded blobs are named.
    // Framework-free so both the API controller (validation) and the storage implementations
    // (filename) share exactly one definition.
    public static class ImageContentType
    {
        public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

        private static readonly Dictionary<string, string> Extensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["image/jpeg"] = ".jpg",
                ["image/png"] = ".png",
                ["image/webp"] = ".webp",
            };

        public static bool IsAllowed(string? contentType) =>
            contentType is not null && Extensions.ContainsKey(contentType);

        public static string ExtensionFor(string contentType) => Extensions[contentType];

        public static string NewFileName(string contentType) =>
            $"{Guid.NewGuid():N}{ExtensionFor(contentType)}";

        // Returns a human-readable error, or null when the upload is acceptable.
        public static string? Validate(long length, string? contentType)
        {
            if (length <= 0) return "No file was uploaded.";
            if (length > MaxBytes) return "Image must be 5 MB or smaller.";
            if (!IsAllowed(contentType)) return "Image must be a JPEG, PNG, or WebP file.";
            return null;
        }
    }
}
