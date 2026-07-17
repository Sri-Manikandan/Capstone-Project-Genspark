using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;

namespace EMSApplicationLayer.Storage
{
    // Development image storage: writes under wwwroot/uploads (served by UseStaticFiles) and
    // returns an ABSOLUTE URL, so it passes the DTOs' [Url] validation and renders directly in
    // <img>. Only registered outside Production. The URL host comes from the current request;
    // during startup seeding there is no request, so it falls back to App:PublicBaseUrl.
    public class LocalImageStorage : IImageStorage
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _http;
        private readonly IConfiguration _config;

        public LocalImageStorage(IWebHostEnvironment env, IHttpContextAccessor http, IConfiguration config)
        {
            _env = env;
            _http = http;
            _config = config;
        }

        public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsDir = Path.Combine(webRoot, "uploads");
            Directory.CreateDirectory(uploadsDir);

            var fileName = ImageContentType.NewFileName(contentType);
            var fullPath = Path.Combine(uploadsDir, fileName);

            await using (var file = File.Create(fullPath))
                await content.CopyToAsync(file, ct);

            var request = _http.HttpContext?.Request;
            var baseUrl = request is not null
                ? $"{request.Scheme}://{request.Host}"
                : (_config["App:PublicBaseUrl"] ?? "http://localhost:5222").TrimEnd('/');
            return $"{baseUrl}/uploads/{fileName}";
        }
    }
}
