using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;

namespace EMSApplicationLayer.Storage
{
    // Development image storage: writes under wwwroot/uploads (served by UseStaticFiles) and
    // returns an ABSOLUTE URL built from the current request, so it passes the DTOs' [Url]
    // validation and renders directly in <img>. Only registered outside Production.
    public class LocalImageStorage : IImageStorage
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _http;

        public LocalImageStorage(IWebHostEnvironment env, IHttpContextAccessor http)
        {
            _env = env;
            _http = http;
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

            var request = _http.HttpContext!.Request;
            return $"{request.Scheme}://{request.Host}/uploads/{fileName}";
        }
    }
}
