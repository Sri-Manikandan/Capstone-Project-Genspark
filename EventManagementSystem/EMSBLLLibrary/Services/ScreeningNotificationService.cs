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
                if (existing.Status == "Active")
                {
                    throw new ValidationException("Already subscribed to notifications for this screening.");
                }

                existing.Status = "Active";
                existing.NotificationCount = 0;
                await _notificationRepo.Update(existing);
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

        public async Task NotifyAvailableTickets(int screeningId)
        {
            var screening = await _screeningRepo.GetById(screeningId);
            if (screening == null) return;

            var ev = await _eventRepo.GetById(screening.EventId);
            if (ev == null) return;

            var activeNotifications = await _notificationRepo.GetActiveNotificationsByScreeningId(screeningId);
            
            var eligibleNotifications = activeNotifications
                .Where(n => n.NotificationCount < 2)
                .ToList();

            foreach (var notification in eligibleNotifications)
            {
                var user = await _userRepo.GetById(notification.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email)) continue;

                var subject = $"Tickets available for {ev.Title}!";
                
                var eventImageHtml = !string.IsNullOrEmpty(ev.ImageUrl) 
                    ? $"<img src=\"{ev.ImageUrl}\" alt=\"{ev.Title}\" style=\"width: 100%; height: auto; max-height: 250px; object-fit: cover;\" />"
                    : "";

                var eventUrl = $"http://localhost:4200/events/{ev.Slug}";

                var body = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 12px; overflow: hidden; background-color: #ffffff;"">
    {eventImageHtml}
    <div style=""padding: 30px;"">
        <h2 style=""color: #4a148c; margin-top: 0; font-size: 24px;"">{ev.Title}</h2>
        <p style=""color: #333333; font-size: 16px; line-height: 1.5;"">Great news, <strong>{user.Name}</strong>!</p>
        <p style=""color: #333333; font-size: 16px; line-height: 1.5;"">Tickets for the <strong>{screening.Screen}</strong> show on <strong>{screening.StartTime:MMM dd, yyyy} at {screening.StartTime:hh:mm tt}</strong> have just become available.</p>
        
        <div style=""text-align: center; margin-top: 35px; margin-bottom: 20px;"">
            <a href=""{eventUrl}"" style=""background-color: #4a148c; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 30px; font-weight: bold; font-size: 16px; display: inline-block;"">Book Your Tickets Now</a>
        </div>
        
        <p style=""color: #888888; font-size: 12px; margin-top: 40px; text-align: center;"">
            You received this email because you subscribed to ticket availability notifications for this showtime.
        </p>
    </div>
</div>";

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
