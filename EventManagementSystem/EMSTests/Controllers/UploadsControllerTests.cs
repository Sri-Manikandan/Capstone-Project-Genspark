using System.Text;
using EMSApplicationLayer.Controllers;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace EMSTests.Controllers
{
    [TestFixture]
    public class UploadsControllerTests
    {
        private static IFormFile FakeFile(string content, string contentType, string fileName = "poster.png")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
        }

        [Test]
        public async Task UploadImage_ValidPng_ReturnsUrlFromStorage()
        {
            var storage = new Mock<IImageStorage>();
            storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
                   .ReturnsAsync("http://localhost:5222/uploads/abc.png");
            var sut = new UploadsController(storage.Object);

            var result = await sut.UploadImage(FakeFile("bytes", "image/png"), CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { url = "http://localhost:5222/uploads/abc.png" });
        }

        [Test]
        public void UploadImage_NullFile_ThrowsValidation()
        {
            var sut = new UploadsController(Mock.Of<IImageStorage>());

            var act = async () => await sut.UploadImage(null, CancellationToken.None);

            act.Should().ThrowAsync<ValidationException>().WithMessage("No file was uploaded.");
        }

        [Test]
        public void UploadImage_WrongType_ThrowsValidation()
        {
            var sut = new UploadsController(Mock.Of<IImageStorage>());

            var act = async () => await sut.UploadImage(FakeFile("bytes", "application/pdf", "x.pdf"), CancellationToken.None);

            act.Should().ThrowAsync<ValidationException>().WithMessage("*JPEG, PNG, or WebP*");
        }
    }
}
