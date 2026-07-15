using EMSBLLLibrary.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class AzureBlobImageStorageTests
    {
        private static IConfiguration Config(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        [Test]
        public void Ctor_MissingConnectionString_Throws()
        {
            var config = Config(new() { ["Storage:ContainerName"] = "event-images" });

            var act = () => new AzureBlobImageStorage(config);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Storage:ConnectionString*");
        }

        [Test]
        public void Ctor_WithConnectionString_DoesNotThrow()
        {
            // A well-formed but fake connection string: the constructor must not touch the network.
            var config = Config(new()
            {
                ["Storage:ConnectionString"] =
                    "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Zm9vYmFy;EndpointSuffix=core.windows.net",
                ["Storage:ContainerName"] = "event-images",
            });

            var act = () => new AzureBlobImageStorage(config);

            act.Should().NotThrow();
        }
    }
}
