using System.Security.Cryptography;
using AutoMapper;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Stripe;

namespace EMSBLLLibrary.Services
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IBookingItemRepository _bookingItemRepo;
        private readonly ITicketTypeRepository _ticketTypeRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly ISeatReservationRepository _reservationRepo;
        private readonly IScreeningRepository _screeningRepo;
        private readonly IEventRepository _eventRepo;
        private readonly ISeatNotifier _notifier;
        private readonly IMapper _mapper;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IStripeRefundClient _refundClient;
        private readonly IUserRepository _userRepo;
        private readonly IEmailQueue _emailQueue;

        public BookingService(
            IBookingRepository bookingRepo,
            IBookingItemRepository bookingItemRepo,
            ITicketTypeRepository ticketTypeRepo,
            ISeatRepository seatRepo,
            ISeatReservationRepository reservationRepo,
            IScreeningRepository screeningRepo,
            IEventRepository eventRepo,
            ISeatNotifier notifier,
            IMapper mapper,
            IPaymentRepository paymentRepo,
            IStripeRefundClient refundClient,
            IUserRepository userRepo,
            IEmailQueue emailQueue)
        {
            _bookingRepo = bookingRepo;
            _bookingItemRepo = bookingItemRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _seatRepo = seatRepo;
            _reservationRepo = reservationRepo;
            _screeningRepo = screeningRepo;
            _eventRepo = eventRepo;
            _notifier = notifier;
            _mapper = mapper;
            _paymentRepo = paymentRepo;
            _refundClient = refundClient;
            _userRepo = userRepo;
            _emailQueue = emailQueue;
        }

        public async Task<BookingDto> Create(int userId, CreateBookingRequest request)
        {
            var screening = await _screeningRepo.GetById(request.ScreeningId)
                ?? throw new NotFoundException($"Screening {request.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.Status != "Published")
                throw new ValidationException("Bookings are only allowed for published events.");

            if (screening.StartTime <= DateTime.UtcNow)
                throw new ValidationException("Bookings are not allowed for screenings that have already started or ended.");

            if (!request.Items.Any())
                throw new ValidationException("At least one ticket item is required.");

            decimal totalAmount = 0;
            // The pending booking holds its seats only as long as their reservations do, so the
            // payment window must end when the earliest seat hold lapses — not outlive it.
            DateTime? holdUntil = null;
            foreach (var item in request.Items)
            {
                var tt = await _ticketTypeRepo.GetById(item.TicketTypeId)
                    ?? throw new NotFoundException($"TicketType {item.TicketTypeId} not found.");

                if (tt.ScreeningId != request.ScreeningId)
                    throw new ValidationException($"TicketType {item.TicketTypeId} does not belong to this screening.");

                if (tt.AvailableQuantity < 1)
                    throw new ValidationException($"Ticket '{tt.Name}' is sold out.");

                var reservation = await _reservationRepo.GetActiveByScreeningAndSeat(request.ScreeningId, item.SeatId);
                if (reservation == null)
                    throw new ValidationException($"Seat {item.SeatId} is not reserved. Please reserve the seat before booking.");

                if (reservation.UserId != userId)
                    throw new ValidationException($"Seat {item.SeatId} is reserved by another user.");

                if (holdUntil == null || reservation.ReservedUntil < holdUntil)
                    holdUntil = reservation.ReservedUntil;

                var seat = await _seatRepo.GetById(item.SeatId)
                    ?? throw new NotFoundException($"Seat {item.SeatId} not found.");

                if (seat.SeatType != tt.SeatType)
                    throw new ValidationException(
                        $"Seat {item.SeatId} is of type '{seat.SeatType}' but ticket '{tt.Name}' requires a '{tt.SeatType}' seat.");

                totalAmount += tt.Price;
            }

            var qrPayload = GenerateQrPayload();
            var booking = new Booking
            {
                UserId = userId,
                ScreeningId = request.ScreeningId,
                BookingReference = GenerateBookingReference(),
                QrCode = QrCodeHelper.GeneratePngBase64(qrPayload),
                QrPayload = qrPayload,
                TotalAmount = totalAmount,
                BookingStatus = "Pending",
                ExpiresAt = holdUntil ?? DateTime.UtcNow
            };
            await _bookingRepo.Add(booking);

            var itemDtos = new List<BookingItemDto>();
            foreach (var itemReq in request.Items)
            {
                var tt = (await _ticketTypeRepo.GetById(itemReq.TicketTypeId))!;
                var seat = await _seatRepo.GetById(itemReq.SeatId);

                var bookingItem = new BookingItem
                {
                    BookingId = booking.Id,
                    TicketTypeId = itemReq.TicketTypeId,
                    SeatId = itemReq.SeatId,
                    UnitPrice = tt.Price,
                    TicketStatus = "Active"
                };
                await _bookingItemRepo.Add(bookingItem);

                if (!await _ticketTypeRepo.TryDecrementAvailableQuantity(itemReq.TicketTypeId))
                    throw new ValidationException($"Ticket '{tt.Name}' is sold out.");

                var reservation = await _reservationRepo.GetActiveByScreeningAndSeat(booking.ScreeningId, itemReq.SeatId);
                if (reservation != null)
                {
                    reservation.Status = "Confirmed";
                    await _reservationRepo.Update(reservation);
                }

                await _notifier.SeatBooked(booking.ScreeningId, itemReq.SeatId);

                var itemDto = _mapper.Map<BookingItemDto>(bookingItem);
                itemDto.TicketTypeName = tt.Name;
                itemDto.SeatLabel = seat != null ? $"{seat.Section}-{seat.Row}{seat.SeatNumber}" : "";
                itemDtos.Add(itemDto);
            }

            var dto = _mapper.Map<BookingDto>(booking);
            dto.EventId = ev.Id;
            dto.EventTitle = ev.Title;
            dto.Screen = screening.Screen;
            dto.ScreeningStartTime = TimeHelper.UtcToIst(screening.StartTime);
            dto.Items = itemDtos;
            return dto;
        }

        public async Task<BookingDto> GetById(int id, int userId)
        {
            var booking = await _bookingRepo.GetById(id)
                ?? throw new NotFoundException($"Booking {id} not found.");

            if (booking.UserId != userId)
                throw new UnauthorizedException("Not authorized to view this booking.");

            return await EnrichBooking(booking);
        }

        public async Task<BookingDto?> GetByReference(string reference, int userId)
        {
            var booking = await _bookingRepo.GetByReference(reference);
            if (booking == null) return null;
            if (booking.UserId != userId)
                throw new UnauthorizedException("Not authorized to view this booking.");
            return await EnrichBooking(booking);
        }

        public async Task<PagedResult<BookingDto>> GetByUserId(int userId, BookingQueryRequest request)
        {
            var (bookings, total) = await _bookingRepo.SearchByUserId(userId, request.Status, request.Page, request.PageSize);
            var items = new List<BookingDto>();
            foreach (var b in bookings)
                items.Add(await EnrichBooking(b));
            return new PagedResult<BookingDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
        }

        public async Task<PagedResult<BookingDto>> GetByEventId(int eventId, int requesterId, bool isAdmin, BookingQueryRequest request)
        {
            var ev = await _eventRepo.GetById(eventId)
                ?? throw new NotFoundException($"Event {eventId} not found.");

            if (!isAdmin && ev.OrganizerId != requesterId)
                throw new UnauthorizedException("Not authorized to view bookings for this event.");

            var (bookings, total) = await _bookingRepo.SearchByEventId(eventId, request.Status, request.Page, request.PageSize);
            var items = new List<BookingDto>();
            foreach (var b in bookings)
                items.Add(await EnrichBooking(b));
            return new PagedResult<BookingDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
        }

        public async Task Cancel(int id, int userId)
        {
            var booking = await _bookingRepo.GetById(id)
                ?? throw new NotFoundException($"Booking {id} not found.");

            if (booking.UserId != userId)
                throw new UnauthorizedException("Not authorized to cancel this booking.");

            if (booking.BookingStatus == "Cancelled")
                throw new ValidationException("Booking is already cancelled.");

            if (booking.BookingStatus == "Attended")
                throw new ValidationException("Cannot cancel a booking that has been attended.");

            var refunded = false;
            if (booking.BookingStatus == "Confirmed")
            {
                var payment = await _paymentRepo.GetByBookingId(id);
                if (payment != null && !string.IsNullOrEmpty(payment.StripePaymentIntentId))
                {
                    await _refundClient.CreateAsync(new RefundCreateOptions
                    {
                        PaymentIntent = payment.StripePaymentIntentId
                    });
                    refunded = true;
                }
            }

            booking.BookingStatus = "Cancelled";
            booking.UpdatedAt = DateTime.UtcNow;
            await _bookingRepo.Update(booking);

            var items = await _bookingItemRepo.GetByBookingId(booking.Id);
            foreach (var item in items)
            {
                item.TicketStatus = "Cancelled";
                await _bookingItemRepo.Update(item);
                await _ticketTypeRepo.IncrementAvailableQuantity(item.TicketTypeId);
                await _notifier.SeatReleased(booking.ScreeningId, item.SeatId);
            }

            await EnqueueBookingCancelled(booking, refunded);
        }

        // Cancellation and refund are one operation, so they are one email. The refund
        // line is rendered in only when a refund was actually issued.
        private async Task EnqueueBookingCancelled(Booking booking, bool refunded)
        {
            var user = await _userRepo.GetById(booking.UserId);
            if (user == null) return;

            var screening = await _screeningRepo.GetById(booking.ScreeningId);
            var ev = screening == null ? null : await _eventRepo.GetById(screening.EventId);

            var refundLine = refunded
                ? $"<p style=\"margin:0 0 24px;\">A refund of &#8377;{booking.TotalAmount:0.00} is on its way back to your original payment method. It usually lands within 5&ndash;10 business days.</p>"
                : "<p style=\"margin:0 0 24px;\">You haven't been charged.</p>";

            await _emailQueue.Enqueue(user.Email, user.Name, EmailTemplateKey.BookingCancelled,
                "Your booking has been cancelled",
                new Dictionary<string, string>
                {
                    ["Name"] = user.Name,
                    ["EventTitle"] = ev?.Title ?? "your event",
                    ["BookingReference"] = booking.BookingReference,
                    ["RefundLine"] = refundLine
                });
        }

        public async Task<BookingDto?> ValidateQr(ValidateQrRequest request, int scannedBy, bool isAdmin)
        {
            var booking = await _bookingRepo.GetByQrPayload(request.QrPayload);

            // Only a Confirmed booking can be scanned; once Attended, the same QR is rejected on any later scan.
            if (booking == null || booking.BookingStatus != "Confirmed")
                return null;

            // An organizer may only scan tickets for events they own.
            if (!isAdmin)
            {
                var screening = await _screeningRepo.GetById(booking.ScreeningId)
                    ?? throw new NotFoundException($"Screening {booking.ScreeningId} not found.");
                var ev = await _eventRepo.GetById(screening.EventId)
                    ?? throw new NotFoundException($"Event {screening.EventId} not found.");
                if (ev.OrganizerId != scannedBy)
                    throw new UnauthorizedException("Not authorized to validate tickets for this event.");
            }

            booking.BookingStatus = "Attended";
            booking.ScannedAt = DateTime.UtcNow;
            booking.ScannedBy = scannedBy;
            booking.UpdatedAt = DateTime.UtcNow;
            await _bookingRepo.Update(booking);
            return await EnrichBooking(booking);
        }

        private async Task<BookingDto> EnrichBooking(Booking booking)
        {
            var screening = await _screeningRepo.GetById(booking.ScreeningId);
            var ev = screening != null ? await _eventRepo.GetById(screening.EventId) : null;
            var items = await _bookingItemRepo.GetByBookingId(booking.Id);

            var itemDtos = new List<BookingItemDto>();
            foreach (var item in items)
            {
                var tt = await _ticketTypeRepo.GetById(item.TicketTypeId);
                var seat = await _seatRepo.GetById(item.SeatId);

                var itemDto = _mapper.Map<BookingItemDto>(item);
                itemDto.TicketTypeName = tt?.Name ?? "";
                itemDto.SeatLabel = seat != null ? $"{seat.Section}-{seat.Row}{seat.SeatNumber}" : "";
                itemDtos.Add(itemDto);
            }

            var dto = _mapper.Map<BookingDto>(booking);
            dto.EventId = ev?.Id ?? 0;
            dto.EventTitle = ev?.Title ?? "";
            dto.Screen = screening?.Screen ?? "";
            dto.ScreeningStartTime = screening != null ? TimeHelper.UtcToIst(screening.StartTime) : default;
            dto.Items = itemDtos;
            return dto;
        }

        private static string GenerateBookingReference() =>
            "BK" + RandomNumberGenerator.GetInt32(10000000, 99999999);

        private static string GenerateQrPayload() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
