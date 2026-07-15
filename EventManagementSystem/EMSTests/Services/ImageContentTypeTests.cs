using EMSBLLLibrary.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class ImageContentTypeTests
    {
        [TestCase("image/jpeg", ".jpg")]
        [TestCase("image/png", ".png")]
        [TestCase("image/webp", ".webp")]
        public void ExtensionFor_ReturnsExpectedExtension(string contentType, string expected)
        {
            ImageContentType.ExtensionFor(contentType).Should().Be(expected);
        }

        [Test]
        public void NewFileName_UsesGuidAndExtension()
        {
            var name = ImageContentType.NewFileName("image/png");
            name.Should().EndWith(".png");
            name.Length.Should().Be(32 + ".png".Length); // 32-char "N" guid + extension
        }

        [Test]
        public void Validate_NullOrEmpty_ReturnsError()
        {
            ImageContentType.Validate(0, "image/png").Should().Be("No file was uploaded.");
        }

        [Test]
        public void Validate_TooLarge_ReturnsError()
        {
            ImageContentType.Validate(ImageContentType.MaxBytes + 1, "image/png")
                .Should().Be("Image must be 5 MB or smaller.");
        }

        [TestCase("application/pdf")]
        [TestCase("image/gif")]
        [TestCase(null)]
        public void Validate_DisallowedType_ReturnsError(string? contentType)
        {
            ImageContentType.Validate(100, contentType)
                .Should().Be("Image must be a JPEG, PNG, or WebP file.");
        }

        [Test]
        public void Validate_ValidJpeg_ReturnsNull()
        {
            ImageContentType.Validate(100, "image/jpeg").Should().BeNull();
        }
    }
}
