using AutoMapper;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class EventServiceTests
    {
        private Mock<IEventRepository> _eventRepo = null!;
        private Mock<IVenueRepository> _venueRepo = null!;
        private IMapper _mapper = null!;
        private EventService _sut = null!;

        // Dates — StartTime must be in the future; EndTime after StartTime
        private static readonly DateTime Start = DateTime.UtcNow.AddDays(10);
        private static readonly DateTime End = DateTime.UtcNow.AddDays(11);

        private const string ValidImageUrl = "https://example.com/image.jpg";
        private const string ValidDescription = "A great event";
        private const string ValidCategory = "Music";

        [SetUp]
        public void SetUp()
        {
            _eventRepo = new Mock<IEventRepository>();
            _venueRepo = new Mock<IVenueRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
            _sut = new EventService(_eventRepo.Object, _venueRepo.Object, _mapper);
        }

        private Event MakeEvent(int id = 1, int organizerId = 10, string status = EventStatus.Draft) => new Event
        {
            Id = id, OrganizerId = organizerId, VenueId = 1,
            Title = "Fest", Status = status, StartTime = Start, EndTime = End, Slug = "fest"
        };

        private CreateEventRequest ValidCreateRequest(int venueId = 1) => new CreateEventRequest
        {
            VenueId = venueId, Title = "Tech Fest",
            Description = ValidDescription, Category = ValidCategory,
            ImageUrl = ValidImageUrl, StartTime = Start, EndTime = End
        };

        private UpdateEventRequest ValidUpdateRequest() => new UpdateEventRequest
        {
            Title = "Updated", Description = ValidDescription, Category = ValidCategory,
            ImageUrl = ValidImageUrl, StartTime = Start, EndTime = End
        };

        // ── Create ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Create_ValidRequest_ReturnsEventDto()
        {
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Venue { Id = 1 });
            _eventRepo.Setup(r => r.GetBySlug(It.IsAny<string>())).ReturnsAsync((Event?)null);
            _eventRepo.Setup(r => r.Add(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Create(10, ValidCreateRequest());

            result.Title.Should().Be("Tech Fest");
        }

        [Test]
        public async Task Create_EndBeforeStart_ThrowsValidationException()
        {
            var req = ValidCreateRequest();
            req.StartTime = End;
            req.EndTime = Start; // before StartTime

            await _sut.Invoking(s => s.Create(10, req))
                .Should().ThrowAsync<ValidationException>().WithMessage("*EndTime*");
        }

        [Test]
        public async Task Create_StartTimeNotInFuture_ThrowsValidationException()
        {
            var req = ValidCreateRequest();
            req.StartTime = DateTime.UtcNow.AddMinutes(-1);
            req.EndTime = DateTime.UtcNow.AddHours(1);

            await _sut.Invoking(s => s.Create(10, req))
                .Should().ThrowAsync<ValidationException>().WithMessage("*StartTime*");
        }

        [Test]
        public async Task Create_EmptyTitle_ThrowsValidationException()
        {
            var req = ValidCreateRequest();
            req.Title = "";

            await _sut.Invoking(s => s.Create(10, req)).Should().ThrowAsync<ValidationException>().WithMessage("*Title*");
        }

        [Test]
        public async Task Create_EmptyDescription_ThrowsValidationException()
        {
            var req = ValidCreateRequest();
            req.Description = "";

            await _sut.Invoking(s => s.Create(10, req)).Should().ThrowAsync<ValidationException>().WithMessage("*Description*");
        }

        [Test]
        public async Task Create_InvalidImageUrl_ThrowsValidationException()
        {
            var req = ValidCreateRequest();
            req.ImageUrl = "not-a-url";

            await _sut.Invoking(s => s.Create(10, req)).Should().ThrowAsync<ValidationException>().WithMessage("*ImageUrl*");
        }

        [Test]
        public async Task Create_VenueNotFound_ThrowsNotFoundException()
        {
            _venueRepo.Setup(r => r.GetById(99)).ReturnsAsync((Venue?)null);

            await _sut.Invoking(s => s.Create(10, ValidCreateRequest(venueId: 99)))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_SlugCollision_GeneratesUniqueSlug()
        {
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Venue { Id = 1 });
            _eventRepo.SetupSequence(r => r.GetBySlug("tech-fest"))
                .ReturnsAsync(MakeEvent())
                .ReturnsAsync((Event?)null);
            _eventRepo.Setup(r => r.Add(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Create(10, ValidCreateRequest());

            result.Slug.Should().Be("tech-fest-1");
        }

        // ── GetById ──────────────────────────────────────────────────────────────

        [Test]
        public async Task GetById_Found_ReturnsDto()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent());

            var result = await _sut.GetById(1);

            result.Id.Should().Be(1);
        }

        [Test]
        public async Task GetById_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.GetById(99)).Should().ThrowAsync<NotFoundException>();
        }

        // ── GetBySlug ────────────────────────────────────────────────────────────

        [Test]
        public async Task GetBySlug_Found_ReturnsDto()
        {
            _eventRepo.Setup(r => r.GetBySlug("fest")).ReturnsAsync(MakeEvent());

            var result = await _sut.GetBySlug("fest");

            result.Should().NotBeNull();
        }

        [Test]
        public async Task GetBySlug_NotFound_ReturnsNull()
        {
            _eventRepo.Setup(r => r.GetBySlug("none")).ReturnsAsync((Event?)null);

            var result = await _sut.GetBySlug("none");

            result.Should().BeNull();
        }

        // ── GetAll / Search / GetByOrganizer ─────────────────────────────────────

        [Test]
        public async Task GetAll_ReturnsMappedList()
        {
            _eventRepo.Setup(r => r.GetAll()).ReturnsAsync(new List<Event> { MakeEvent() });

            var result = await _sut.GetAll();

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task Search_ReturnsMappedList()
        {
            _eventRepo.Setup(r => r.Search(null, null, null, null, null, null, null, 1, 10))
                      .ReturnsAsync((new List<Event> { MakeEvent() }, 1));

            var result = await _sut.Search(new EventSearchRequest { Page = 1, PageSize = 10 });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Test]
        public async Task GetByOrganizer_ReturnsMappedList()
        {
            _eventRepo.Setup(r => r.GetByOrganizerId(10, 1, 10))
                      .ReturnsAsync((new List<Event> { MakeEvent() }, 1));

            var result = await _sut.GetByOrganizer(10, 1, 10);

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        // ── Update ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Update_OwnDraftEvent_ReturnsUpdatedDto()
        {
            var ev = MakeEvent(status: EventStatus.Draft);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Update(1, 10, ValidUpdateRequest());

            result.Title.Should().Be("Updated");
        }

        [Test]
        public async Task Update_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Update(99, 10, ValidUpdateRequest()))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(organizerId: 10));

            await _sut.Invoking(s => s.Update(1, 99, ValidUpdateRequest()))
                .Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Update_PendingApproval_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.PendingApproval));

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest()))
                .Should().ThrowAsync<ValidationException>().WithMessage("*pending admin approval*");
        }

        [Test]
        public async Task Update_Published_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.Published));

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest()))
                .Should().ThrowAsync<ValidationException>().WithMessage("*published*");
        }

        [Test]
        public async Task Update_EndBeforeStart_ThrowsValidationException()
        {
            var ev = MakeEvent(status: EventStatus.Draft);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            var req = ValidUpdateRequest();
            req.StartTime = End;
            req.EndTime = Start; // before StartTime

            await _sut.Invoking(s => s.Update(1, 10, req))
                .Should().ThrowAsync<ValidationException>().WithMessage("*EndTime*");
        }

        [Test]
        public async Task Update_EmptyTitle_ThrowsValidationException()
        {
            var ev = MakeEvent(status: EventStatus.Draft);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            var req = ValidUpdateRequest();
            req.Title = "";

            await _sut.Invoking(s => s.Update(1, 10, req)).Should().ThrowAsync<ValidationException>().WithMessage("*Title*");
        }

        // ── Delete ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Delete_OwnEvent_CallsDelete()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent());
            _eventRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);

            await _sut.Delete(1, 10);

            _eventRepo.Verify(r => r.Delete(1), Times.Once);
        }

        [Test]
        public async Task Delete_AdminBypass_CallsDelete()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(organizerId: 10));
            _eventRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);

            await _sut.Delete(1, 99, isAdmin: true);

            _eventRepo.Verify(r => r.Delete(1), Times.Once);
        }

        [Test]
        public async Task Delete_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Delete(99, 10)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Delete_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(organizerId: 10));

            await _sut.Invoking(s => s.Delete(1, 99)).Should().ThrowAsync<UnauthorizedException>();
        }

        // ── Submit ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Submit_OrganizerDraftEvent_SetsToPendingApproval()
        {
            var ev = MakeEvent(status: EventStatus.Draft);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Submit(1, 10);

            result.Status.Should().Be(EventStatus.PendingApproval);
        }

        [Test]
        public async Task Submit_RejectedEvent_SetsToPendingApproval()
        {
            var ev = MakeEvent(status: EventStatus.Rejected);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Submit(1, 10);

            result.Status.Should().Be(EventStatus.PendingApproval);
        }

        [Test]
        public async Task Submit_Admin_SetsToPublished()
        {
            var ev = MakeEvent(status: EventStatus.Draft);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Submit(1, 99, isAdmin: true);

            result.Status.Should().Be(EventStatus.Published);
        }

        [Test]
        public async Task Submit_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Submit(99, 10)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Submit_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(organizerId: 10));

            await _sut.Invoking(s => s.Submit(1, 99)).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Submit_PublishedEvent_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.Published));

            await _sut.Invoking(s => s.Submit(1, 10)).Should().ThrowAsync<ValidationException>();
        }

        // ── Cancel ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Cancel_OwnEvent_SetsToCancelled()
        {
            var ev = MakeEvent(status: EventStatus.Draft);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Cancel(1, 10);

            result.Status.Should().Be(EventStatus.Cancelled);
        }

        [Test]
        public async Task Cancel_AdminBypass_SetsToCancelled()
        {
            var ev = MakeEvent(status: EventStatus.Published, organizerId: 10);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.Cancel(1, 99, isAdmin: true);

            result.Status.Should().Be(EventStatus.Cancelled);
        }

        [Test]
        public async Task Cancel_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Cancel(99, 10)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Cancel_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(organizerId: 10));

            await _sut.Invoking(s => s.Cancel(1, 99)).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Cancel_AlreadyCancelled_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.Cancelled));

            await _sut.Invoking(s => s.Cancel(1, 10)).Should().ThrowAsync<ValidationException>().WithMessage("*already cancelled*");
        }

        // ── Admin operations ─────────────────────────────────────────────────────

        [Test]
        public async Task GetPendingApproval_ReturnsMappedList()
        {
            _eventRepo.Setup(r => r.GetByStatus(EventStatus.PendingApproval)).ReturnsAsync(new List<Event> { MakeEvent() });

            var result = await _sut.GetPendingApproval();

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task AdminApprove_PendingEvent_SetsToPublished()
        {
            var ev = MakeEvent(status: EventStatus.PendingApproval);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.AdminApprove(1);

            result.Status.Should().Be(EventStatus.Published);
        }

        [Test]
        public async Task AdminApprove_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.AdminApprove(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task AdminApprove_AlreadyPublished_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.Published));

            await _sut.Invoking(s => s.AdminApprove(1)).Should().ThrowAsync<ValidationException>().WithMessage("*already published*");
        }

        [Test]
        public async Task AdminApprove_CancelledEvent_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.Cancelled));

            await _sut.Invoking(s => s.AdminApprove(1)).Should().ThrowAsync<ValidationException>().WithMessage("*cancelled*");
        }

        [Test]
        public async Task AdminReject_PendingEvent_SetsToRejected()
        {
            var ev = MakeEvent(status: EventStatus.PendingApproval);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);

            var result = await _sut.AdminReject(1, "Spam");

            result.Status.Should().Be(EventStatus.Rejected);
        }

        [Test]
        public async Task AdminReject_NotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.AdminReject(99, null)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task AdminReject_NotPending_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeEvent(status: EventStatus.Draft));

            await _sut.Invoking(s => s.AdminReject(1, null)).Should().ThrowAsync<ValidationException>().WithMessage("*not pending*");
        }
    }
}
