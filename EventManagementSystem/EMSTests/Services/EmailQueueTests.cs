using System.Text.Json;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class EmailQueueTests
    {
        private Mock<IEmailOutboxRepository> _outboxRepo;
        private EmailQueue _sut;

        [SetUp]
        public void SetUp()
        {
            _outboxRepo = new Mock<IEmailOutboxRepository>();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:AppBaseUrl"] = "http://localhost:4200"
                })
                .Build();

            _sut = new EmailQueue(_outboxRepo.Object, config);
        }

        [Test]
        public async Task Enqueue_ShouldAddPendingRow_WithTemplateKeyAndPayload()
        {
            EmailOutbox? captured = null;
            _outboxRepo.Setup(r => r.Add(It.IsAny<EmailOutbox>()))
                .Callback<EmailOutbox>(o => captured = o)
                .ReturnsAsync((EmailOutbox o) => o);

            await _sut.Enqueue("a@b.com", "Asha", EmailTemplateKey.Welcome, "Welcome to EventHub",
                new Dictionary<string, string> { ["Name"] = "Asha" });

            captured.Should().NotBeNull();
            captured!.ToEmail.Should().Be("a@b.com");
            captured.TemplateKey.Should().Be(EmailTemplateKey.Welcome);
            captured.Status.Should().Be(EmailStatus.Pending);
            captured.Subject.Should().Be("Welcome to EventHub");
        }

        // AppUrl and Year are needed by every template. Injecting them centrally means
        // no call site can forget them and ship a raw {{AppUrl}} to a user.
        [Test]
        public async Task Enqueue_ShouldInjectAppUrlAndYearTokens()
        {
            EmailOutbox? captured = null;
            _outboxRepo.Setup(r => r.Add(It.IsAny<EmailOutbox>()))
                .Callback<EmailOutbox>(o => captured = o)
                .ReturnsAsync((EmailOutbox o) => o);

            await _sut.Enqueue("a@b.com", "Asha", EmailTemplateKey.Welcome, "Welcome",
                new Dictionary<string, string> { ["Name"] = "Asha" });

            var tokens = JsonSerializer.Deserialize<Dictionary<string, string>>(captured!.PayloadJson)!;
            tokens["AppUrl"].Should().Be("http://localhost:4200");
            tokens["Year"].Should().Be(TimeHelper.UtcToIst(DateTime.UtcNow).Year.ToString());
            tokens["Name"].Should().Be("Asha");
        }

        [Test]
        public async Task Enqueue_ShouldNotThrow_WhenRepositoryFails()
        {
            _outboxRepo.Setup(r => r.Add(It.IsAny<EmailOutbox>()))
                .ThrowsAsync(new Exception("db down"));

            var act = async () => await _sut.Enqueue("a@b.com", "Asha", EmailTemplateKey.Welcome, "Welcome",
                new Dictionary<string, string> { ["Name"] = "Asha" });

            await act.Should().NotThrowAsync("a failed email must never fail the domain operation");
        }

        [Test]
        public async Task EnqueueMany_ShouldDelegateToAddMany()
        {
            List<EmailOutbox>? captured = null;
            _outboxRepo.Setup(r => r.AddMany(It.IsAny<List<EmailOutbox>>()))
                .Callback<List<EmailOutbox>>(o => captured = o)
                .Returns(Task.CompletedTask);

            await _sut.EnqueueMany(new List<QueuedEmail>
            {
                new("a@b.com", "A", EmailTemplateKey.EventReminder, "Tomorrow",
                    new Dictionary<string, string> { ["Name"] = "A" }, "reminder:booking:1", null),
                new("c@d.com", "C", EmailTemplateKey.EventReminder, "Tomorrow",
                    new Dictionary<string, string> { ["Name"] = "C" }, "reminder:booking:2", null)
            });

            captured.Should().HaveCount(2);
            captured![0].DedupeKey.Should().Be("reminder:booking:1");
        }

        [Test]
        public async Task EnqueueMany_ShouldDoNothing_WhenListEmpty()
        {
            await _sut.EnqueueMany(new List<QueuedEmail>());

            _outboxRepo.Verify(r => r.AddMany(It.IsAny<List<EmailOutbox>>()), Times.Never);
        }
    }
}
