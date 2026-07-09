using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class ScreeningNotificationService : IScreeningNotificationService
    {
        private readonly IScreeningNotificationRepository _notificationRepo;
        private readonly IScreeningRepository _screeningRepo;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepo;
        private readonly IEventRepository _eventRepo;

        public ScreeningNotificationService(
            IScreeningNotificationRepository notificationRepo,
            IScreeningRepository screeningRepo,
            IEmailService emailService,
            IUserRepository userRepo,
            IEventRepository eventRepo)
        {
            _notificationRepo = notificationRepo;
            _screeningRepo = screeningRepo;
            _emailService = emailService;
            _userRepo = userRepo;
            _eventRepo = eventRepo;
        }

        public async Task Subscribe(int screeningId, int userId)
        {
            var screening = await _screeningRepo.GetById(screeningId)
                ?? throw new NotFoundException($"Screening {screeningId} not found.");

            var existing = await _notificationRepo.GetByScreeningAndUserId(screeningId, userId);
            if (existing != null)
            {
                if (existing.Status != "Active")
                {
                    existing.Status = "Active";
                    await _notificationRepo.Update(existing);
                }
                return;
            }

            var notification = new ScreeningNotification
            {
                ScreeningId = screeningId,
                UserId = userId,
                NotificationCount = 0,
                Status = "Active"
            };

            await _notificationRepo.Add(notification);
        }

        public async Task Unsubscribe(int screeningId, int userId)
        {
            var existing = await _notificationRepo.GetByScreeningAndUserId(screeningId, userId);
            if (existing != null && existing.Status == "Active")
            {
                existing.Status = "Cancelled";
                await _notificationRepo.Update(existing);
            }
        }

        public async Task NotifyAvailableTickets(int screeningId)
        {
            var screening = await _screeningRepo.GetById(screeningId);
            if (screening == null) return;

            var ev = await _eventRepo.GetById(screening.EventId);
            if (ev == null) return;

            var activeNotifications = await _notificationRepo.GetActiveNotificationsByScreeningId(screeningId);
            
            // Only notify users who have received less than 2 emails
            var eligibleNotifications = activeNotifications
                .Where(n => n.NotificationCount < 2)
                .ToList();

            foreach (var notification in eligibleNotifications)
            {
                var user = await _userRepo.GetById(notification.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email)) continue;

                var subject = $"Tickets available for {ev.Title}!";
                var body = $"<p>Great news!</p><p>Tickets are now available for <strong>{ev.Title}</strong>.</p><p>Click here to book before they sell out again!</p>";

                await _emailService.SendEmailAsync(user.Email, subject, body);

                notification.NotificationCount++;
                
                // If they have received 2 emails, cap it and mark as Completed
                if (notification.NotificationCount >= 2)
                {
                    notification.Status = "Completed";
                }

                await _notificationRepo.Update(notification);
            }
        }
    }
}
