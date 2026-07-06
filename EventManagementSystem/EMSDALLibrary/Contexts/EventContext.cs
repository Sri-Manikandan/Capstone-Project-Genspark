using EMSDALLibrary.Constants;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace EMSDALLibrary.Contexts
{
    public class EventContext : DbContext
    {
        private readonly ICurrentUserAccessor? _currentUser;

        // Property values never written to the audit trail.
        private static readonly HashSet<string> SensitiveProperties = new()
        {
            "PasswordHash",
            "Token"
        };

        public EventContext(DbContextOptions<EventContext> options, ICurrentUserAccessor? currentUser = null)
            : base(options)
        {
            _currentUser = currentUser;
        }

        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<Screening> Screenings { get; set; } = null!;
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
        public DbSet<ChangeLog> ChangeLogs { get; set; } = null!;

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

            modelBuilder.Entity<Screening>(e =>
            {
                e.HasOne<Event>().WithMany().HasForeignKey(s => s.EventId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(s => s.EventId);
                e.HasIndex(s => s.StartTime);
            });

            modelBuilder.Entity<Seat>(e =>
            {
                e.HasOne<Venue>().WithMany().HasForeignKey(s => s.VenueId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TicketType>(e =>
            {
                e.HasOne<Screening>().WithMany().HasForeignKey(tt => tt.ScreeningId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(tt => tt.ScreeningId);
                e.Property(tt => tt.Price).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Booking>(e =>
            {
                e.HasIndex(b => b.BookingReference).IsUnique();
                e.HasIndex(b => new { b.BookingStatus, b.ExpiresAt });
                e.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Screening>().WithMany().HasForeignKey(b => b.ScreeningId).OnDelete(DeleteBehavior.Restrict);
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
                e.HasOne<Screening>().WithMany().HasForeignKey(sr => sr.ScreeningId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<User>().WithMany().HasForeignKey(sr => sr.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(sr => new { sr.ScreeningId, sr.SeatId })
                    .IsUnique()
                    .HasFilter("\"Status\" = 'Active'");
                e.HasIndex(sr => sr.ScreeningId);
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

            modelBuilder.Entity<ChangeLog>(e =>
            {
                e.Property(c => c.Changes).HasColumnType("jsonb");
                e.HasIndex(c => new { c.EntityName, c.EntityKey });
                e.HasIndex(c => c.CreatedAt);
                e.HasIndex(c => c.UserId);
            });
        }

        // ── Changelog audit trail ────────────────────────────────────────────────

        public override int SaveChanges()
        {
            var audits = CaptureAudits();
            if (audits.Count == 0)
                return base.SaveChanges();

            var result = base.SaveChanges();
            WriteAudits(audits);
            base.SaveChanges();
            return result;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var audits = CaptureAudits();
            if (audits.Count == 0)
                return await base.SaveChangesAsync(cancellationToken);

            var result = await base.SaveChangesAsync(cancellationToken);
            WriteAudits(audits);
            await base.SaveChangesAsync(cancellationToken);
            return result;
        }

        private List<PendingAudit> CaptureAudits()
        {
            var userId = _currentUser?.GetUserId();
            if (userId is null)
                return new List<PendingAudit>();

            ChangeTracker.DetectChanges();
            var role = _currentUser?.GetUserRole();
            var audits = new List<PendingAudit>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is ChangeLog)
                    continue;
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                    continue;

                audits.Add(new PendingAudit
                {
                    Entry = entry,
                    EntityName = entry.Metadata.ClrType.Name,
                    Action = entry.State switch
                    {
                        EntityState.Added => ChangeAction.Create,
                        EntityState.Deleted => ChangeAction.Delete,
                        _ => ChangeAction.Update
                    },
                    KeyPending = entry.State == EntityState.Added,
                    Key = entry.State == EntityState.Added ? null : BuildKey(entry),
                    Changes = BuildChanges(entry),
                    UserId = userId.Value,
                    UserRole = role
                });
            }

            return audits;
        }

        private void WriteAudits(List<PendingAudit> audits)
        {
            foreach (var audit in audits)
            {
                ChangeLogs.Add(new ChangeLog
                {
                    EntityName = audit.EntityName,
                    EntityKey = audit.KeyPending ? BuildKey(audit.Entry) : audit.Key!,
                    Action = audit.Action,
                    Changes = audit.Changes,
                    UserId = audit.UserId,
                    UserRole = audit.UserRole,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private static string BuildKey(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key is null)
                return string.Empty;
            var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "");
            return string.Join(",", values);
        }

        private static string BuildChanges(EntityEntry entry)
        {
            var changes = new Dictionary<string, object?>();

            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey())
                    continue;

                var name = prop.Metadata.Name;
                var redacted = SensitiveProperties.Contains(name);

                switch (entry.State)
                {
                    case EntityState.Added:
                        changes[name] = new Dictionary<string, object?>
                        {
                            ["old"] = null,
                            ["new"] = redacted ? "***" : prop.CurrentValue
                        };
                        break;

                    case EntityState.Deleted:
                        changes[name] = new Dictionary<string, object?>
                        {
                            ["old"] = redacted ? "***" : prop.OriginalValue,
                            ["new"] = null
                        };
                        break;

                    case EntityState.Modified:
                        if (!prop.IsModified || Equals(prop.OriginalValue, prop.CurrentValue))
                            continue;
                        changes[name] = new Dictionary<string, object?>
                        {
                            ["old"] = redacted ? "***" : prop.OriginalValue,
                            ["new"] = redacted ? "***" : prop.CurrentValue
                        };
                        break;
                }
            }

            return JsonSerializer.Serialize(changes);
        }

        private sealed class PendingAudit
        {
            public EntityEntry Entry { get; init; } = null!;
            public string EntityName { get; init; } = string.Empty;
            public string Action { get; init; } = string.Empty;
            public bool KeyPending { get; init; }
            public string? Key { get; init; }
            public string Changes { get; init; } = "{}";
            public int UserId { get; init; }
            public string? UserRole { get; init; }
        }
    }
}
