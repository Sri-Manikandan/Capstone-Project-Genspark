using EMSApplicationLayer.BackgroundServices;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Emails;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class EmailDispatcherServiceTests
    {
        private Mock<IEmailOutboxRepository> _outboxRepo;
        private Mock<IEmailSender> _sender;
        private Mock<IEmailTemplateRenderer> _renderer;
        private EmailDispatcher _sut;

        [SetUp]
        public void SetUp()
        {
            _outboxRepo = new Mock<IEmailOutboxRepository>();
            _sender = new Mock<IEmailSender>();
            _renderer = new Mock<IEmailTemplateRenderer>();

            _renderer.Setup(r => r.Render(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                .Returns("<html>rendered</html>");

            _sut = new EmailDispatcher(_outboxRepo.Object, _sender.Object, _renderer.Object,
                NullLogger<EmailDispatcher>.Instance);
        }

        private static EmailOutbox Pending(int attempts = 0) => new()
        {
            Id = 1,
            ToEmail = "a@b.com",
            ToName = "Asha",
            TemplateKey = EmailTemplateKey.Welcome,
            Subject = "Welcome",
            PayloadJson = "{\"Name\":\"Asha\"}",
            Status = EmailStatus.Pending,
            Attempts = attempts
        };

        private void GivenPending(EmailOutbox row) =>
            _outboxRepo.Setup(r => r.GetPendingBatch(It.IsAny<int>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<EmailOutbox> { row });

        private void GivenSendReturns(EmailSendResult result) =>
            _sender.Setup(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<List<EmailAttachment>>()))
                .ReturnsAsync(result);

        [Test]
        public async Task DispatchOnce_ShouldMarkSent_WhenSendSucceeds()
        {
            var row = Pending();
            GivenPending(row);
            GivenSendReturns(EmailSendResult.Ok("msg_123"));

            await _sut.DispatchOnce(CancellationToken.None);

            row.Status.Should().Be(EmailStatus.Sent);
            row.ProviderMessageId.Should().Be("msg_123");
            row.SentAt.Should().NotBeNull();
        }

        [Test]
        public async Task DispatchOnce_ShouldBackOff_WhenSendIsTransientFailure()
        {
            var row = Pending();
            GivenPending(row);
            var before = row.SendAfter;
            GivenSendReturns(EmailSendResult.Transient("503 unavailable"));

            await _sut.DispatchOnce(CancellationToken.None);

            row.Status.Should().Be(EmailStatus.Pending);
            row.Attempts.Should().Be(1);
            row.LastError.Should().Contain("503");
            row.SendAfter.Should().BeAfter(before);
        }

        // A bad address will fail identically forever. Retrying it five times just
        // delays every other email in the queue.
        [Test]
        public async Task DispatchOnce_ShouldFailImmediately_WhenSendIsPermanentFailure()
        {
            var row = Pending();
            GivenPending(row);
            GivenSendReturns(EmailSendResult.Permanent("422 invalid address"));

            await _sut.DispatchOnce(CancellationToken.None);

            row.Status.Should().Be(EmailStatus.Failed);
            row.Attempts.Should().Be(1);
        }

        [Test]
        public async Task DispatchOnce_ShouldGiveUp_AfterMaxAttempts()
        {
            var row = Pending(attempts: 4);
            GivenPending(row);
            GivenSendReturns(EmailSendResult.Transient("503 unavailable"));

            await _sut.DispatchOnce(CancellationToken.None);

            row.Attempts.Should().Be(5);
            row.Status.Should().Be(EmailStatus.Failed);
        }

        [Test]
        public async Task DispatchOnce_ShouldAttachQrTicket_WhenPayloadCarriesOne()
        {
            var row = Pending();
            row.TemplateKey = EmailTemplateKey.BookingConfirmed;
            row.PayloadJson = "{\"Name\":\"Asha\",\"BookingReference\":\"BK1\",\"QrBase64\":\"aGVsbG8=\"}";
            GivenPending(row);

            List<EmailAttachment>? captured = null;
            _sender.Setup(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<List<EmailAttachment>>()))
                .Callback<string, string, string, string, List<EmailAttachment>>((_, _, _, _, a) => captured = a)
                .ReturnsAsync(EmailSendResult.Ok("msg_1"));

            await _sut.DispatchOnce(CancellationToken.None);

            captured.Should().ContainSingle();
            captured![0].FileName.Should().Be("ticket-BK1.png");
            captured[0].ContentBase64.Should().Be("aGVsbG8=");
        }

        // QrBase64 is an attachment, not body copy. If it leaked into the token map it
        // would be substituted into the HTML as a giant base64 blob.
        [Test]
        public async Task DispatchOnce_ShouldNotPassQrBase64ToRenderer()
        {
            var row = Pending();
            row.TemplateKey = EmailTemplateKey.BookingConfirmed;
            row.PayloadJson = "{\"Name\":\"Asha\",\"BookingReference\":\"BK1\",\"QrBase64\":\"aGVsbG8=\"}";
            GivenPending(row);

            IDictionary<string, string>? captured = null;
            _renderer.Setup(r => r.Render(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                .Callback<string, IDictionary<string, string>>((_, t) => captured = t)
                .Returns("<html></html>");
            GivenSendReturns(EmailSendResult.Ok("msg_1"));

            await _sut.DispatchOnce(CancellationToken.None);

            captured.Should().NotContainKey("QrBase64");
        }

        [Test]
        public async Task DispatchOnce_ShouldFailRow_WhenTemplateMissing()
        {
            var row = Pending();
            GivenPending(row);
            _renderer.Setup(r => r.Render(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                .Throws(new InvalidOperationException("template not found"));

            await _sut.DispatchOnce(CancellationToken.None);

            row.Status.Should().Be(EmailStatus.Failed);
            _sender.Verify(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<EmailAttachment>>()), Times.Never);
        }
    }
}
