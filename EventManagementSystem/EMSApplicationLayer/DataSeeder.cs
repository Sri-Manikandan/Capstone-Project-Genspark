using EMSDALLibrary.Contexts;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSApplicationLayer;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventContext>();

        if (await db.Users.AnyAsync()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword("Test@1234");
        var now  = DateTime.UtcNow;

        // ── 1. Users ──────────────────────────────────────────────────────────
        var admin = new User { Name = "System Admin",  Email = "admin@ems.com",    Phone = "9000000001", PasswordHash = hash, Role = "Admin",     IsActive = true };
        var alice = new User { Name = "Alice Johnson", Email = "alice@ems.com",    Phone = "9000000002", PasswordHash = hash, Role = "Organizer", IsActive = true };
        var bob   = new User { Name = "Bob Smith",     Email = "bob@ems.com",      Phone = "9000000003", PasswordHash = hash, Role = "Organizer", IsActive = true };
        var carol = new User { Name = "Carol White",   Email = "carol@ems.com",    Phone = "9000000004", PasswordHash = hash, Role = "Organizer", IsActive = true };
        var david = new User { Name = "David Brown",   Email = "david@ems.com",    Phone = "9000000005", PasswordHash = hash, Role = "User",      IsActive = true };
        var emma  = new User { Name = "Emma Wilson",   Email = "emma@ems.com",     Phone = "9000000006", PasswordHash = hash, Role = "User",      IsActive = true };
        var frank = new User { Name = "Frank Miller",  Email = "frank@ems.com",    Phone = "9000000007", PasswordHash = hash, Role = "User",      IsActive = true };
        var grace = new User { Name = "Grace Lee",     Email = "grace@ems.com",    Phone = "9000000008", PasswordHash = hash, Role = "User",      IsActive = true };
        var henry = new User { Name = "Henry Chen",    Email = "henry@ems.com",    Phone = "9000000009", PasswordHash = hash, Role = "User",      IsActive = true };
        var ivan  = new User { Name = "Ivan Petrov",   Email = "ivan@ems.com",     Phone = "9000000010", PasswordHash = hash, Role = "User",      IsActive = false };

        db.Users.AddRange(admin, alice, bob, carol, david, emma, frank, grace, henry, ivan);
        await db.SaveChangesAsync();

        // ── 2. OrganizerRequests ──────────────────────────────────────────────
        db.OrganizerRequests.AddRange(
            new OrganizerRequest { UserId = alice.Id, Status = "Approved", Reason = "Verified professional event organizer.",         RequestedAt = now.AddDays(-30), ReviewedAt = now.AddDays(-29), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = bob.Id,   Status = "Approved", Reason = "Experienced in large-scale events.",             RequestedAt = now.AddDays(-25), ReviewedAt = now.AddDays(-24), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = carol.Id, Status = "Approved", Reason = "Strong portfolio of past events.",               RequestedAt = now.AddDays(-20), ReviewedAt = now.AddDays(-19), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = david.Id, Status = "Pending",  RequestedAt = now.AddDays(-3) },
            new OrganizerRequest { UserId = emma.Id,  Status = "Rejected", Reason = "Portfolio did not meet requirements.",           RequestedAt = now.AddDays(-10), ReviewedAt = now.AddDays(-8),  ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = henry.Id, Status = "Pending",  RequestedAt = now.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // ── 3. Venues ─────────────────────────────────────────────────────────
        var grandArena = new Venue
        {
            Name = "Grand Arena", Address = "1 Arena Blvd", City = "Chennai", TotalCapacity = 500,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":4,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":3,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":2,\"seatsPerRow\":10}]}"
        };
        var cityConvention = new Venue
        {
            Name = "City Convention Center", Address = "200 Convention St", City = "Mumbai", TotalCapacity = 300,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":3,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":2,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":1,\"seatsPerRow\":10}]}"
        };
        var riversideHall = new Venue
        {
            Name = "Riverside Hall", Address = "5 River Rd", City = "Bengaluru", TotalCapacity = 200,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":2,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":1,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":1,\"seatsPerRow\":5}]}"
        };
        db.Venues.AddRange(grandArena, cityConvention, riversideHall);
        await db.SaveChangesAsync();

        // ── 4. Seats ──────────────────────────────────────────────────────────
        var grandArenaSeats   = GenerateSeats(grandArena.Id,    ("A", "Silver", 4, 10), ("B", "Gold", 3, 10), ("C", "Premium", 2, 10));
        var citySeats         = GenerateSeats(cityConvention.Id, ("A", "Silver", 3, 10), ("B", "Gold", 2, 10), ("C", "Premium", 1, 10));
        var riversideSeats    = GenerateSeats(riversideHall.Id,  ("A", "Silver", 2, 10), ("B", "Gold", 1, 10), ("C", "Premium", 1, 5));

        db.Seats.AddRange(grandArenaSeats);
        db.Seats.AddRange(citySeats);
        db.Seats.AddRange(riversideSeats);
        await db.SaveChangesAsync();

        var gaSilver  = grandArenaSeats.Where(s => s.SeatType == "Silver").ToList();
        var gaGold    = grandArenaSeats.Where(s => s.SeatType == "Gold").ToList();
        var gaPremium = grandArenaSeats.Where(s => s.SeatType == "Premium").ToList();
        var ccSilver  = citySeats.Where(s => s.SeatType == "Silver").ToList();
        var ccGold    = citySeats.Where(s => s.SeatType == "Gold").ToList();
        var ccPremium = citySeats.Where(s => s.SeatType == "Premium").ToList();
        var rhSilver  = riversideSeats.Where(s => s.SeatType == "Silver").ToList();
        var rhGold    = riversideSeats.Where(s => s.SeatType == "Gold").ToList();

        // ── 5. Events ─────────────────────────────────────────────────────────
        var techSummit = new Event
        {
            OrganizerId = alice.Id, VenueId = grandArena.Id, Title = "Tech Summit 2026",
            Description = "India's premier technology conference featuring speakers from top tech companies, with workshops on AI, cloud, and emerging tech.",
            Status = "Published", Category = "Technology", Slug = "tech-summit-2026",
            StartTime = now.AddDays(65), EndTime = now.AddDays(66),
            ImageUrl = "https://picsum.photos/seed/techsummit/800/400"
        };
        var melodyFest = new Event
        {
            OrganizerId = alice.Id, VenueId = cityConvention.Id, Title = "Melody Fest 2026",
            Description = "A three-day music extravaganza celebrating genres from classical to EDM, with 50+ artists performing live.",
            Status = "Published", Category = "Music", Slug = "melody-fest-2026",
            StartTime = now.AddDays(90), EndTime = now.AddDays(93),
            ImageUrl = "https://picsum.photos/seed/melodyfest/800/400"
        };
        var artShow = new Event
        {
            OrganizerId = bob.Id, VenueId = riversideHall.Id, Title = "Contemporary Art Show",
            Description = "Showcasing 200+ artworks by emerging and established artists from across South Asia.",
            Status = "Published", Category = "Art", Slug = "contemporary-art-show-2026",
            StartTime = now.AddDays(38), EndTime = now.AddDays(40),
            ImageUrl = "https://picsum.photos/seed/artshow/800/400"
        };
        var startupPitch = new Event
        {
            OrganizerId = bob.Id, VenueId = grandArena.Id, Title = "Startup Pitch Night",
            Description = "Top 20 startups pitch to a panel of investors and veterans for seed funding opportunities.",
            Status = "Published", Category = "Business", Slug = "startup-pitch-night-2026",
            StartTime = now.AddDays(115), EndTime = now.AddDays(115),
            ImageUrl = "https://picsum.photos/seed/startup/800/400"
        };
        var comedyGala = new Event
        {
            OrganizerId = carol.Id, VenueId = cityConvention.Id, Title = "Comedy Gala Night",
            Description = "A star-studded evening of stand-up comedy featuring India's top comedians and surprise international guests.",
            Status = "Published", Category = "Entertainment", Slug = "comedy-gala-night-2026",
            StartTime = now.AddDays(150), EndTime = now.AddDays(150),
            ImageUrl = "https://picsum.photos/seed/comedy/800/400"
        };
        var foodFest = new Event
        {
            OrganizerId = carol.Id, VenueId = riversideHall.Id, Title = "Food & Culture Festival",
            Description = "A vibrant celebration of regional cuisines and cultural performances from 25 Indian states.",
            Status = "Pending", Category = "Food", Slug = "food-culture-festival-2026",
            StartTime = now.AddDays(180), EndTime = now.AddDays(182),
            ImageUrl = "https://picsum.photos/seed/foodfest/800/400"
        };
        var wellnessDraft = new Event
        {
            OrganizerId = alice.Id, VenueId = grandArena.Id, Title = "Wellness Workshop",
            Description = "Full-day mindfulness and wellness workshop with certified practitioners.",
            Status = "Draft", Category = "Health", Slug = "wellness-workshop-2026",
            StartTime = now.AddDays(200), EndTime = now.AddDays(200),
            ImageUrl = "https://picsum.photos/seed/wellness/800/400"
        };
        var rejectedTalk = new Event
        {
            OrganizerId = bob.Id, VenueId = grandArena.Id, Title = "Rejected Tech Talk",
            Description = "A short tech talk session.",
            Status = "Rejected", Category = "Technology", Slug = "rejected-tech-talk-2026",
            StartTime = now.AddDays(50), EndTime = now.AddDays(50),
            ImageUrl = "https://picsum.photos/seed/techtalk/800/400",
            RejectionReason = "Insufficient details. Please provide speaker bios and agenda."
        };
        var cancelledConf = new Event
        {
            OrganizerId = carol.Id, VenueId = cityConvention.Id, Title = "Cancelled Design Conference",
            Description = "A conference on UI/UX design trends — cancelled due to venue unavailability.",
            Status = "Cancelled", Category = "Design", Slug = "cancelled-design-conference-2026",
            StartTime = now.AddDays(45), EndTime = now.AddDays(45),
            ImageUrl = "https://picsum.photos/seed/design/800/400"
        };

        db.Events.AddRange(techSummit, melodyFest, artShow, startupPitch, comedyGala, foodFest, wellnessDraft, rejectedTalk, cancelledConf);
        await db.SaveChangesAsync();

        // ── 6. TicketTypes ────────────────────────────────────────────────────
        var saleStart = now.AddDays(-7);

        var ttTechSilver    = new TicketType { EventId = techSummit.Id,   Name = "Silver",  SeatType = "Silver",  Price = 500m,  TotalQuantity = 40, AvailableQuantity = 40, SaleStart = saleStart, SaleEnd = now.AddDays(60),  IsActive = true };
        var ttTechGold      = new TicketType { EventId = techSummit.Id,   Name = "Gold",    SeatType = "Gold",    Price = 800m,  TotalQuantity = 30, AvailableQuantity = 30, SaleStart = saleStart, SaleEnd = now.AddDays(60),  IsActive = true };
        var ttTechPremium   = new TicketType { EventId = techSummit.Id,   Name = "Premium", SeatType = "Premium", Price = 1200m, TotalQuantity = 20, AvailableQuantity = 20, SaleStart = saleStart, SaleEnd = now.AddDays(60),  IsActive = true };

        var ttMelodySilver  = new TicketType { EventId = melodyFest.Id,   Name = "Silver",  SeatType = "Silver",  Price = 300m, TotalQuantity = 30, AvailableQuantity = 30, SaleStart = saleStart, SaleEnd = now.AddDays(85),  IsActive = true };
        var ttMelodyGold    = new TicketType { EventId = melodyFest.Id,   Name = "Gold",    SeatType = "Gold",    Price = 500m, TotalQuantity = 20, AvailableQuantity = 20, SaleStart = saleStart, SaleEnd = now.AddDays(85),  IsActive = true };
        var ttMelodyPremium = new TicketType { EventId = melodyFest.Id,   Name = "Premium", SeatType = "Premium", Price = 750m, TotalQuantity = 10, AvailableQuantity = 10, SaleStart = saleStart, SaleEnd = now.AddDays(85),  IsActive = true };

        var ttArtSilver     = new TicketType { EventId = artShow.Id,      Name = "Silver",  SeatType = "Silver",  Price = 200m, TotalQuantity = 20, AvailableQuantity = 20, SaleStart = saleStart, SaleEnd = now.AddDays(35),  IsActive = true };
        var ttArtGold       = new TicketType { EventId = artShow.Id,      Name = "Gold",    SeatType = "Gold",    Price = 350m, TotalQuantity = 10, AvailableQuantity = 10, SaleStart = saleStart, SaleEnd = now.AddDays(35),  IsActive = true };
        var ttArtPremium    = new TicketType { EventId = artShow.Id,      Name = "Premium", SeatType = "Premium", Price = 500m, TotalQuantity = 5,  AvailableQuantity = 5,  SaleStart = saleStart, SaleEnd = now.AddDays(35),  IsActive = true };

        var ttStartupSilver  = new TicketType { EventId = startupPitch.Id, Name = "Silver",  SeatType = "Silver",  Price = 1000m, TotalQuantity = 40, AvailableQuantity = 40, SaleStart = saleStart, SaleEnd = now.AddDays(110), IsActive = true };
        var ttStartupGold    = new TicketType { EventId = startupPitch.Id, Name = "Gold",    SeatType = "Gold",    Price = 1500m, TotalQuantity = 30, AvailableQuantity = 30, SaleStart = saleStart, SaleEnd = now.AddDays(110), IsActive = true };
        var ttStartupPremium = new TicketType { EventId = startupPitch.Id, Name = "Premium", SeatType = "Premium", Price = 2000m, TotalQuantity = 20, AvailableQuantity = 20, SaleStart = saleStart, SaleEnd = now.AddDays(110), IsActive = true };

        var ttComedySilver   = new TicketType { EventId = comedyGala.Id,   Name = "Silver",  SeatType = "Silver",  Price = 400m, TotalQuantity = 30, AvailableQuantity = 30, SaleStart = saleStart, SaleEnd = now.AddDays(145), IsActive = true };
        var ttComedyGold     = new TicketType { EventId = comedyGala.Id,   Name = "Gold",    SeatType = "Gold",    Price = 600m, TotalQuantity = 20, AvailableQuantity = 20, SaleStart = saleStart, SaleEnd = now.AddDays(145), IsActive = true };
        var ttComedyPremium  = new TicketType { EventId = comedyGala.Id,   Name = "Premium", SeatType = "Premium", Price = 800m, TotalQuantity = 10, AvailableQuantity = 10, SaleStart = saleStart, SaleEnd = now.AddDays(145), IsActive = true };

        db.TicketTypes.AddRange(
            ttTechSilver, ttTechGold, ttTechPremium,
            ttMelodySilver, ttMelodyGold, ttMelodyPremium,
            ttArtSilver, ttArtGold, ttArtPremium,
            ttStartupSilver, ttStartupGold, ttStartupPremium,
            ttComedySilver, ttComedyGold, ttComedyPremium);
        await db.SaveChangesAsync();

        // ── 7. Bookings ───────────────────────────────────────────────────────
        // Seat index counters (each pointer advances as seats are consumed)
        int gaS = 0, gaG = 0, gaP = 0;
        int ccS = 0, ccG = 0, ccP = 0;
        int rhS = 0, rhG = 0;

        var booking1  = MakeBooking("BK-2026-001001", david.Id, techSummit.Id,   "Confirmed", 1300m,  now.AddDays(64));
        var booking2  = MakeBooking("BK-2026-001002", emma.Id,  techSummit.Id,   "Confirmed", 1200m,  now.AddDays(64));
        var booking3  = MakeBooking("BK-2026-002001", frank.Id, melodyFest.Id,   "Confirmed", 600m,   now.AddDays(89));
        var booking4  = MakeBooking("BK-2026-003001", grace.Id, artShow.Id,      "Completed", 350m,   now.AddDays(37),  scannedAt: now.AddDays(-1), scannedBy: alice.Id);
        var booking5  = MakeBooking("BK-2026-004001", henry.Id, startupPitch.Id, "Confirmed", 1500m,  now.AddDays(114));
        var booking6  = MakeBooking("BK-2026-005001", david.Id, comedyGala.Id,   "Pending",   400m,   now.AddHours(1));
        var booking7  = MakeBooking("BK-2026-002002", emma.Id,  melodyFest.Id,   "Cancelled", 500m,   now.AddDays(89));
        var booking8  = MakeBooking("BK-2026-001003", frank.Id, techSummit.Id,   "Expired",   500m,   now.AddHours(-2));
        var booking9  = MakeBooking("BK-2026-005002", grace.Id, comedyGala.Id,   "Confirmed", 800m,   now.AddDays(149));
        var booking10 = MakeBooking("BK-2026-003002", henry.Id, artShow.Id,      "Cancelled", 200m,   now.AddDays(37));

        db.Bookings.AddRange(booking1, booking2, booking3, booking4, booking5, booking6, booking7, booking8, booking9, booking10);
        await db.SaveChangesAsync();

        // ── 8. BookingItems ───────────────────────────────────────────────────
        db.BookingItems.AddRange(
            // booking1: David → Tech Summit (Silver + Gold, Confirmed)
            Item(booking1.Id, ttTechSilver.Id,    gaSilver[gaS++].Id,  500m,  "Sold"),
            Item(booking1.Id, ttTechGold.Id,      gaGold[gaG++].Id,    800m,  "Sold"),
            // booking2: Emma → Tech Summit (Premium, Confirmed)
            Item(booking2.Id, ttTechPremium.Id,   gaPremium[gaP++].Id, 1200m, "Sold"),
            // booking3: Frank → Melody Fest (2x Silver, Confirmed)
            Item(booking3.Id, ttMelodySilver.Id,  ccSilver[ccS++].Id,  300m,  "Sold"),
            Item(booking3.Id, ttMelodySilver.Id,  ccSilver[ccS++].Id,  300m,  "Sold"),
            // booking4: Grace → Art Show (Gold, Completed)
            Item(booking4.Id, ttArtGold.Id,       rhGold[rhG++].Id,    350m,  "Sold"),
            // booking5: Henry → Startup Pitch (Gold, Confirmed)
            Item(booking5.Id, ttStartupGold.Id,   gaGold[gaG++].Id,    1500m, "Sold"),
            // booking6: David → Comedy Gala (Silver, Pending)
            Item(booking6.Id, ttComedySilver.Id,  ccSilver[ccS++].Id,  400m,  "Reserved"),
            // booking7: Emma → Melody Fest (Gold, Cancelled)
            Item(booking7.Id, ttMelodyGold.Id,    ccGold[ccG++].Id,    500m,  "Cancelled"),
            // booking8: Frank → Tech Summit (Silver, Expired)
            Item(booking8.Id, ttTechSilver.Id,    gaSilver[gaS++].Id,  500m,  "Cancelled"),
            // booking9: Grace → Comedy Gala (Premium, Confirmed)
            Item(booking9.Id, ttComedyPremium.Id, ccPremium[ccP++].Id, 800m,  "Sold"),
            // booking10: Henry → Art Show (Silver, Cancelled)
            Item(booking10.Id, ttArtSilver.Id,    rhSilver[rhS++].Id,  200m,  "Cancelled")
        );
        await db.SaveChangesAsync();

        // ── 9. Payments ───────────────────────────────────────────────────────
        db.Payments.AddRange(
            new Payment { BookingId = booking1.Id, StripePaymentIntentId = "pi_seed_001", StripeChargeId = "ch_seed_001", StripeCustomerId = "cus_seed_david",  Amount = 1300m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-5) },
            new Payment { BookingId = booking2.Id, StripePaymentIntentId = "pi_seed_002", StripeChargeId = "ch_seed_002", StripeCustomerId = "cus_seed_emma",   Amount = 1200m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-4) },
            new Payment { BookingId = booking3.Id, StripePaymentIntentId = "pi_seed_003", StripeChargeId = "ch_seed_003", StripeCustomerId = "cus_seed_frank",  Amount = 600m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-3) },
            new Payment { BookingId = booking4.Id, StripePaymentIntentId = "pi_seed_004", StripeChargeId = "ch_seed_004", StripeCustomerId = "cus_seed_grace",  Amount = 350m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-8) },
            new Payment { BookingId = booking5.Id, StripePaymentIntentId = "pi_seed_005", StripeChargeId = "ch_seed_005", StripeCustomerId = "cus_seed_henry",  Amount = 1500m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-2) },
            new Payment { BookingId = booking9.Id, StripePaymentIntentId = "pi_seed_009", StripeChargeId = "ch_seed_009", StripeCustomerId = "cus_seed_grace2", Amount = 800m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // ── 10. SeatReservations ──────────────────────────────────────────────
        db.SeatReservations.AddRange(
            new SeatReservation { SeatId = gaSilver[gaS++].Id,  TicketTypeId = ttTechSilver.Id,     EventId = techSummit.Id,   UserId = carol.Id,  Status = "Active",   ReservedUntil = now.AddMinutes(8) },
            new SeatReservation { SeatId = ccPremium[ccP++].Id, TicketTypeId = ttMelodyPremium.Id,  EventId = melodyFest.Id,   UserId = henry.Id,  Status = "Active",   ReservedUntil = now.AddMinutes(5) },
            new SeatReservation { SeatId = rhSilver[rhS++].Id,  TicketTypeId = ttArtSilver.Id,      EventId = artShow.Id,      UserId = david.Id,  Status = "Released", ReservedUntil = now.AddMinutes(-5) },
            new SeatReservation { SeatId = ccGold[ccG++].Id,    TicketTypeId = ttComedyGold.Id,     EventId = comedyGala.Id,   UserId = emma.Id,   Status = "Expired",  ReservedUntil = now.AddMinutes(-15) },
            new SeatReservation { SeatId = gaPremium[gaP++].Id, TicketTypeId = ttStartupPremium.Id, EventId = startupPitch.Id, UserId = frank.Id,  Status = "Active",   ReservedUntil = now.AddMinutes(9) }
        );
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static EMSModelLibrary.Models.Booking MakeBooking(
        string reference, int userId, int eventId, string status, decimal total, DateTime expiresAt,
        DateTime? scannedAt = null, int? scannedBy = null) => new()
    {
        BookingReference = reference,
        QrCode           = "QRC-" + reference,
        QrPayload        = "{\"ref\":\"" + reference + "\",\"eventId\":" + eventId + ",\"userId\":" + userId + "}",
        UserId           = userId,
        EventId          = eventId,
        BookingStatus    = status,
        TotalAmount      = total,
        ExpiresAt        = expiresAt,
        ScannedAt        = scannedAt,
        ScannedBy        = scannedBy
    };

    private static BookingItem Item(int bookingId, int ticketTypeId, int seatId, decimal price, string status) => new()
    {
        BookingId    = bookingId,
        TicketTypeId = ticketTypeId,
        SeatId       = seatId,
        UnitPrice    = price,
        TicketStatus = status
    };

    private static List<Seat> GenerateSeats(int venueId, params (string Section, string SeatType, int Rows, int SeatsPerRow)[] sections)
    {
        var seats = new List<Seat>();
        foreach (var (section, seatType, rows, seatsPerRow) in sections)
            for (var r = 0; r < rows; r++)
            {
                var row = ((char)('A' + r)).ToString();
                for (var n = 1; n <= seatsPerRow; n++)
                    seats.Add(new Seat { VenueId = venueId, Section = section, Row = row, SeatNumber = n, SeatType = seatType });
            }
        return seats;
    }
}
