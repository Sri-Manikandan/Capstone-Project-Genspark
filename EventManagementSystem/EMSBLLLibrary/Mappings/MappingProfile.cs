using AutoMapper;
using EMSBLLLibrary.Helpers;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            CreateMap<Venue, VenueDto>()
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            CreateMap<Seat, SeatDto>();

            // City and VenueName require a venue lookup — set manually after mapping
            CreateMap<Event, EventDto>()
                .ForMember(d => d.StartTime, o => o.MapFrom(s => TimeHelper.UtcToIst(s.StartTime)))
                .ForMember(d => d.EndTime, o => o.MapFrom(s => TimeHelper.UtcToIst(s.EndTime)))
                .ForMember(d => d.City, o => o.Ignore())
                .ForMember(d => d.VenueName, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            CreateMap<TicketType, TicketTypeDto>()
                .ForMember(d => d.SaleStart, o => o.MapFrom(s => TimeHelper.UtcToIst(s.SaleStart)))
                .ForMember(d => d.SaleEnd, o => o.MapFrom(s => TimeHelper.UtcToIst(s.SaleEnd)))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            CreateMap<Payment, PaymentDto>()
                .ForMember(d => d.PaidAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.PaidAt)))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            CreateMap<SeatReservation, SeatReservationDto>()
                .ForMember(d => d.ReservedUntil, o => o.MapFrom(s => TimeHelper.UtcToIst(s.ReservedUntil)))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            // EventTitle and Items require async DB lookups — set manually after mapping
            CreateMap<Booking, BookingDto>()
                .ForMember(dest => dest.EventTitle, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(d => d.ExpiresAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.ExpiresAt)))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => TimeHelper.UtcToIst(s.CreatedAt)));

            // TicketTypeName and SeatLabel require async DB lookups — set manually after mapping
            CreateMap<BookingItem, BookingItemDto>()
                .ForMember(dest => dest.TicketTypeName, opt => opt.Ignore())
                .ForMember(dest => dest.SeatLabel, opt => opt.Ignore());
        }
    }
}
