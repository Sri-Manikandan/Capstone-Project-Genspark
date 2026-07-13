using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Emails;
using FluentAssertions;
using NUnit.Framework;

namespace EMSTests.Emails
{
    [TestFixture]
    public class EmailTemplateRendererTests
    {
        private EmailTemplateRenderer _sut;

        [SetUp]
        public void SetUp() => _sut = new EmailTemplateRenderer();

        [Test]
        public void Render_ShouldSubstituteTokens()
        {
            var html = _sut.Render(EmailTemplateKey.Welcome, new Dictionary<string, string>
            {
                ["Name"] = "Asha",
                ["AppUrl"] = "http://localhost:4200",
                ["Year"] = "2026"
            });

            html.Should().Contain("Asha");
        }

        [Test]
        public void Render_ShouldWrapContentInLayout()
        {
            var html = _sut.Render(EmailTemplateKey.Welcome, new Dictionary<string, string>
            {
                ["Name"] = "Asha",
                ["AppUrl"] = "http://localhost:4200",
                ["Year"] = "2026"
            });

            html.Should().StartWith("<!DOCTYPE html>");
            html.Should().Contain("EventHub");
        }

        // The bug this whole test class exists to catch: a typo'd or forgotten token
        // shipping literal "{{Something}}" to a real user.
        [TestCaseSource(nameof(AllTemplates))]
        public void Render_ShouldLeaveNoUnreplacedTokens(string templateKey, Dictionary<string, string> tokens)
        {
            var html = _sut.Render(templateKey, tokens);

            html.Should().NotContain("{{", because: $"template {templateKey} must have every token supplied");
        }

        [Test]
        public void Render_ShouldThrow_WhenTemplateNotFound()
        {
            var act = () => _sut.Render("NoSuchTemplate", new Dictionary<string, string>());

            act.Should().Throw<InvalidOperationException>();
        }

        public static IEnumerable<TestCaseData> AllTemplates()
        {
            // Every template implicitly gets AppUrl and Year from EmailQueue, so every
            // case here supplies them too.
            static Dictionary<string, string> With(params (string Key, string Value)[] extra)
            {
                var tokens = new Dictionary<string, string>
                {
                    ["AppUrl"] = "http://localhost:4200",
                    ["Year"] = "2026"
                };
                foreach (var (key, value) in extra)
                    tokens[key] = value;
                return tokens;
            }

            yield return new TestCaseData(EmailTemplateKey.Welcome,
                With(("Name", "A")));

            yield return new TestCaseData(EmailTemplateKey.PasswordReset,
                With(("Name", "A"), ("ResetUrl", "u"), ("ExpiryMinutes", "15")));

            yield return new TestCaseData(EmailTemplateKey.BookingConfirmed,
                With(("Name", "A"), ("EventTitle", "E"), ("VenueName", "V"), ("ScreeningTime", "T"),
                     ("BookingReference", "R"), ("TotalAmount", "100.00"), ("ItemsHtml", "<tr></tr>")));

            yield return new TestCaseData(EmailTemplateKey.PaymentFailed,
                With(("Name", "A"), ("EventTitle", "E"), ("BookingReference", "R")));

            yield return new TestCaseData(EmailTemplateKey.BookingCancelled,
                With(("Name", "A"), ("EventTitle", "E"), ("BookingReference", "R"), ("RefundLine", "")));

            yield return new TestCaseData(EmailTemplateKey.BookingExpired,
                With(("Name", "A"), ("EventTitle", "E"), ("BookingReference", "R")));

            yield return new TestCaseData(EmailTemplateKey.OrganizerRequestApproved,
                With(("Name", "A")));

            yield return new TestCaseData(EmailTemplateKey.OrganizerRequestRejected,
                With(("Name", "A"), ("Reason", "R")));

            yield return new TestCaseData(EmailTemplateKey.EventApproved,
                With(("Name", "A"), ("EventTitle", "E")));

            yield return new TestCaseData(EmailTemplateKey.EventRejected,
                With(("Name", "A"), ("EventTitle", "E"), ("Reason", "R")));

            yield return new TestCaseData(EmailTemplateKey.AdminReviewPending,
                With(("Name", "A"), ("ItemType", "Event"), ("ItemTitle", "E")));

            yield return new TestCaseData(EmailTemplateKey.ScreeningRescheduled,
                With(("Name", "A"), ("EventTitle", "E"), ("BookingReference", "R"),
                     ("OldTime", "T1"), ("NewTime", "T2"), ("VenueName", "V")));

            yield return new TestCaseData(EmailTemplateKey.EventReminder,
                With(("Name", "A"), ("EventTitle", "E"), ("ScreeningTime", "T"),
                     ("VenueName", "V"), ("BookingReference", "R")));
        }
    }
}
