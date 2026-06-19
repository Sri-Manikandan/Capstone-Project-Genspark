using System.Security.Cryptography;
using AutoMapper;
using EMSModelLibrary.DTOs;
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
        private readonly IEventRepository _eventRepo;
        private readonly ISeatNotifier _notifier;
        private readonly IMapper _mapper;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IStripeRefundClient _refundClient;

        public BookingService(
            IBookingRepository bookingRepo,
            IBookingItemRepository bookingItemRepo,
            ITicketTypeRepository ticketTypeRepo,
            ISeatRepository seatRepo,
            ISeatReservationRepository reservationRepo,
            IEventRepository eventRepo,
            ISeatNotifier notifier,
            IMapper mapper,
            IPaymentRepository paymentRepo,
            IStripeRefundClient refundClient)
        {
            _bookingRepo = bookingRepo;
            _bookingItemRepo = bookingItemRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _seatRepo = seatRepo;
            _reservationRepo = reservationRepo;
            _eventRepo = eventRepo;
            _notifier = notifier;
            _mapper = mapper;
            _paymentRepo = paymentRepo;
            _refundClient = refundClient;
        }

        public async Task<BookingDto> Create(int userId, CreateBookingRequest request)
        {
            var ev = await _eventRepo.GetById(request.EventId)
                ?? throw new NotFoundException($"Event {request.EventId} not found.");

            if (ev.Status != "Published")
                throw new ValidationException("Bookings are only allowed for published events.");

            if (ev.StartTime <= DateTime.UtcNow)
                throw new ValidationException("Bookings are not allowed for events that have already started or ended.");

            if (!request.Items.Any())
                throw new ValidationException("At least one ticket item is required.");

            decimal totalAmount = 0;
            foreach (var item in request.Items)
            {
                var tt = await _ticketTypeRepo.GetById(item.TicketTypeId)
                    ?? throw new NotFoundException($"TicketType {item.TicketTypeId} not found.");

                if (tt.EventId != request.EventId)
                    throw new ValidationException($"TicketType {item.TicketTypeId} does not belong to this event.");

                if (tt.AvailableQuantity < 1)
                    throw new ValidationException($"Ticket '{tt.Name}' is sold out.");

                var reservation = await _reservationRepo.GetActiveByEventAndSeat(request.EventId, item.SeatId);
                if (reservation == null)
                    throw new ValidationException($"Seat {item.SeatId} is not reserved. Please reserve the seat before booking.");

                if (reservation.UserId != userId)
                    throw new ValidationException($"Seat {item.SeatId} is reserved by another user.");

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
                EventId = request.EventId,
                BookingReference = GenerateBookingReference(),
                QrCode = qrPayload,
                QrPayload = qrPayload,
                TotalAmount = totalAmount,
                BookingStatus = "Pending",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
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

                var reservation = await _reservationRepo.GetActiveByEventAndSeat(booking.EventId, itemReq.SeatId);
                if (reservation != null)
                {
                    reservation.Status = "Confirmed";
                    await _reservationRepo.Update(reservation);
                }

                await _notifier.SeatBooked(booking.EventId, itemReq.SeatId);

                var itemDto = _mapper.Map<BookingItemDto>(bookingItem);
                itemDto.TicketTypeName = tt.Name;
                itemDto.SeatLabel = seat != null ? $"{seat.Section}-{seat.Row}{seat.SeatNumber}" : "";
                itemDtos.Add(itemDto);
            }

            var dto = _mapper.Map<BookingDto>(booking);
            dto.EventTitle = ev.Title;
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

        public async Task<PagedResult<BookingDto>> GetByEventId(int eventId, BookingQueryRequest request)
        {
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
            if (booking.BookingStatus == "Confirmed")
            {
                var payment = await _paymentRepo.GetByBookingId(id);
                if (payment != null && !string.IsNullOrEmpty(payment.StripePaymentIntentId))
                {
                    await _refundClient.CreateAsync(new RefundCreateOptions
                    {
                        PaymentIntent = payment.StripePaymentIntentId
                    });
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
                await _notifier.SeatReleased(booking.EventId, item.SeatId);
            }
        }

        public async Task<bool> ValidateQr(ValidateQrRequest request)
        {
            var all = await _bookingRepo.GetAll();
            var booking = all.FirstOrDefault(b => b.QrPayload == request.QrPayload);

            if (booking == null || booking.BookingStatus != "Confirmed")
                return false;

            booking.BookingStatus = "Attended";
            booking.ScannedAt = DateTime.UtcNow;
            booking.ScannedBy = request.ScannedBy;
            booking.UpdatedAt = DateTime.UtcNow;
            await _bookingRepo.Update(booking);
            return true;
        }

        private async Task<BookingDto> EnrichBooking(Booking booking)
        {
            var ev = await _eventRepo.GetById(booking.EventId);
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
            dto.EventTitle = ev?.Title ?? "";
            dto.Items = itemDtos;
            return dto;
        }

        private static string GenerateBookingReference() =>
            "BK" + RandomNumberGenerator.GetInt32(10000000, 99999999);

        private static string GenerateQrPayload() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
