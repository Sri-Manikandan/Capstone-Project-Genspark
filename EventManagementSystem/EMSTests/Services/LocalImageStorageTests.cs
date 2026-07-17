using System.Text;
using EMSApplicationLayer.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class LocalImageStorageTests
    {
        private string _webRoot = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _webRoot = Path.Combine(Path.GetTempPath(), "ems-local-storage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_webRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
        }

        private Mock<IWebHostEnvironment> Env()
        {
            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.WebRootPath).Returns(_webRoot);
            env.SetupGet(e => e.ContentRootPath).Returns(_webRoot);
            return env;
        }

        [Test]
        public async Task SaveAsync_WithRequest_UsesRequestHostForUrl()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("localhost:5222");
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(httpContext);

            var config = new Mock<IConfiguration>();
            var sut = new LocalImageStorage(Env().Object, accessor.Object, config.Object);

            using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));
            var url = await sut.SaveAsync(content, "image/png");

            url.Should().StartWith("http://localhost:5222/uploads/");
            url.Should().EndWith(".png");

            var fileName = url.Split('/').Last();
            File.Exists(Path.Combine(_webRoot, "uploads", fileName)).Should().BeTrue();
        }

        [Test]
        public async Task SaveAsync_WithoutRequest_FallsBackToPublicBaseUrl()
        {
            // Startup seeding has no HttpContext, so the host must come from App:PublicBaseUrl.
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);

            var config = new Mock<IConfiguration>();
            config.SetupGet(c => c["App:PublicBaseUrl"]).Returns("http://seed-host:5222/");

            var sut = new LocalImageStorage(Env().Object, accessor.Object, config.Object);

            using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));
            var url = await sut.SaveAsync(content, "image/jpeg");

            url.Should().StartWith("http://seed-host:5222/uploads/");
            url.Should().EndWith(".jpg");

            var fileName = url.Split('/').Last();
            File.Exists(Path.Combine(_webRoot, "uploads", fileName)).Should().BeTrue();
        }
    }
}
