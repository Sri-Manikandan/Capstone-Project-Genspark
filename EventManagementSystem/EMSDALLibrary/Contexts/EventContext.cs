using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Contexts
{
    public class EventContext : DbContext
    {
        public EventContext(DbContextOptions<EventContext> options) : base(options) { }

        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<Venue> Venues { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Seat> Seats { get; set; } = null!;
        public DbSet<TicketType> TicketTypes { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<BookingItem> BookingItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<SeatReservation> SeatReservations { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<OrganizerRequest> OrganizerRequests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.PasswordHash).IsRequired();
            });

            modelBuilder.Entity<Event>(e =>
            {
                e.HasIndex(ev => ev.Slug).IsUnique();
                e.HasIndex(ev => ev.Status);
                e.HasIndex(ev => ev.Category);
                e.HasIndex(ev => ev.StartTime);
                e.HasOne<User>().WithMany().HasForeignKey(ev => ev.OrganizerId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Venue>().WithMany().HasForeignKey(ev => ev.VenueId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Seat>(e =>
            {
                e.HasOne<Venue>().WithMany().HasForeignKey(s => s.VenueId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TicketType>(e =>
            {
                e.HasOne<Event>().WithMany().HasForeignKey(tt => tt.EventId).OnDelete(DeleteBehavior.Cascade);
                e.Property(tt => tt.Price).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Booking>(e =>
            {
                e.HasIndex(b => b.BookingReference).IsUnique();
                e.HasIndex(b => new { b.BookingStatus, b.ExpiresAt });
                e.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Event>().WithMany().HasForeignKey(b => b.EventId).OnDelete(DeleteBehavior.Restrict);
                e.Property(b => b.TotalAmount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<BookingItem>(e =>
            {
                e.HasOne<Booking>().WithMany().HasForeignKey(bi => bi.BookingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<TicketType>().WithMany().HasForeignKey(bi => bi.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Seat>().WithMany().HasForeignKey(bi => bi.SeatId).OnDelete(DeleteBehavior.Restrict);
                e.Property(bi => bi.UnitPrice).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Payment>(e =>
            {
                e.HasOne<Booking>().WithMany().HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(p => p.StripePaymentIntentId);
                e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<SeatReservation>(e =>
            {
                e.HasOne<Seat>().WithMany().HasForeignKey(sr => sr.SeatId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<Event>().WithMany().HasForeignKey(sr => sr.EventId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<User>().WithMany().HasForeignKey(sr => sr.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(sr => new { sr.EventId, sr.SeatId })
                    .IsUnique()
                    .HasFilter("\"Status\" = 'Active'");
                e.HasIndex(sr => sr.EventId);
                e.HasIndex(sr => new { sr.Status, sr.ReservedUntil });
            });

            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.HasIndex(rt => rt.Token).IsUnique();
                e.HasOne<User>().WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OrganizerRequest>(e =>
            {
                e.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.Status);
            });
        }
    }
}
