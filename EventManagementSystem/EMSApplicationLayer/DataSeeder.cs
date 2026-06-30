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

        // Real, themed images (each verified reachable). Reused within a category.
        const string imgConcert1 = "https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?w=800&q=80";
        const string imgConcert2 = "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?w=800&q=80";
        const string imgConcert3 = "https://images.unsplash.com/photo-1501386761578-eac5c94b800a?w=800&q=80";
        const string imgConcert4 = "https://images.unsplash.com/photo-1470229722913-7c0e2dbbafd3?w=800&q=80";
        const string imgConcert5 = "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?w=800&q=80";
        const string imgConcert6 = "https://images.unsplash.com/photo-1429962714451-bb934ecdc4ec?w=800&q=80";
        const string imgComedy1  = "https://images.unsplash.com/photo-1527224538127-2104bb71c51b?w=800&q=80";
        const string imgComedy2  = "https://images.unsplash.com/photo-1585699324551-f6c309eedeca?w=800&q=80";
        const string imgComedy3  = "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=800&q=80";
        const string imgMovie1   = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=800&q=80";
        const string imgMovie2   = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=800&q=80";
        const string imgMovie3   = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=800&q=80";

        // ── 3. Venues (Tamil Nadu) ────────────────────────────────────────────
        var nehruStadium = new Venue
        {
            Name = "Nehru Indoor Stadium", Address = "Sydenhams Road, Periamet", City = "Chennai", TotalCapacity = 500,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":4,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":3,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":2,\"seatsPerRow\":10}]}"
        };
        var sathyamCinema = new Venue
        {
            Name = "Sathyam Cinemas", Address = "8 Thiruvika Road, Royapettah", City = "Chennai", TotalCapacity = 400,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":4,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":3,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":2,\"seatsPerRow\":10}]}"
        };
        var codissia = new Venue
        {
            Name = "Codissia Trade Fair Complex", Address = "Trade Fair Road, Peelamedu", City = "Coimbatore", TotalCapacity = 300,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":3,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":2,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":1,\"seatsPerRow\":10}]}"
        };
        var tamukkam = new Venue
        {
            Name = "Tamukkam Grounds", Address = "Tamukkam Road, Aringnar Anna Nagar", City = "Madurai", TotalCapacity = 250,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":3,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":2,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":1,\"seatsPerRow\":10}]}"
        };
        var annaAuditorium = new Venue
        {
            Name = "Anna Auditorium", Address = "Anna Nagar, Thillai Nagar", City = "Tiruchirappalli", TotalCapacity = 200,
            LayoutConfig = "{\"sections\":[{\"name\":\"A\",\"type\":\"Silver\",\"rows\":2,\"seatsPerRow\":10},{\"name\":\"B\",\"type\":\"Gold\",\"rows\":1,\"seatsPerRow\":10},{\"name\":\"C\",\"type\":\"Premium\",\"rows\":1,\"seatsPerRow\":5}]}"
        };
        db.Venues.AddRange(nehruStadium, sathyamCinema, codissia, tamukkam, annaAuditorium);
        await db.SaveChangesAsync();

        // ── 4. Seats ──────────────────────────────────────────────────────────
        var nehruSeats   = GenerateSeats(nehruStadium.Id,   ("A", "Silver", 4, 10), ("B", "Gold", 3, 10), ("C", "Premium", 2, 10));
        var sathyamSeats = GenerateSeats(sathyamCinema.Id,  ("A", "Silver", 4, 10), ("B", "Gold", 3, 10), ("C", "Premium", 2, 10));
        var codissiaSeats = GenerateSeats(codissia.Id,      ("A", "Silver", 3, 10), ("B", "Gold", 2, 10), ("C", "Premium", 1, 10));
        var tamukkamSeats = GenerateSeats(tamukkam.Id,      ("A", "Silver", 3, 10), ("B", "Gold", 2, 10), ("C", "Premium", 1, 10));
        var annaSeats    = GenerateSeats(annaAuditorium.Id, ("A", "Silver", 2, 10), ("B", "Gold", 1, 10), ("C", "Premium", 1, 5));

        db.Seats.AddRange(nehruSeats);
        db.Seats.AddRange(sathyamSeats);
        db.Seats.AddRange(codissiaSeats);
        db.Seats.AddRange(tamukkamSeats);
        db.Seats.AddRange(annaSeats);
        await db.SaveChangesAsync();

        var nhSilver  = nehruSeats.Where(s => s.SeatType == "Silver").ToList();
        var nhGold    = nehruSeats.Where(s => s.SeatType == "Gold").ToList();
        var nhPremium = nehruSeats.Where(s => s.SeatType == "Premium").ToList();
        var sySilver  = sathyamSeats.Where(s => s.SeatType == "Silver").ToList();
        var syGold    = sathyamSeats.Where(s => s.SeatType == "Gold").ToList();
        var syPremium = sathyamSeats.Where(s => s.SeatType == "Premium").ToList();
        var cdSilver  = codissiaSeats.Where(s => s.SeatType == "Silver").ToList();
        var cdGold    = codissiaSeats.Where(s => s.SeatType == "Gold").ToList();
        var tmSilver  = tamukkamSeats.Where(s => s.SeatType == "Silver").ToList();
        var tmGold    = tamukkamSeats.Where(s => s.SeatType == "Gold").ToList();
        var tmPremium = tamukkamSeats.Where(s => s.SeatType == "Premium").ToList();
        var anSilver  = annaSeats.Where(s => s.SeatType == "Silver").ToList();
        var anGold    = annaSeats.Where(s => s.SeatType == "Gold").ToList();

        // ── 5. Events ─────────────────────────────────────────────────────────
        Event Ev(int organizerId, int venueId, string title, string description, string status, string category,
                 string slug, int startInDays, string imageUrl, string screen = "", string? rejectionReason = null) => new()
        {
            OrganizerId = organizerId, VenueId = venueId, Title = title, Description = description,
            Status = status, Category = category, Slug = slug,
            StartTime = now.AddDays(startInDays), EndTime = now.AddDays(startInDays).AddHours(3),
            ImageUrl = imageUrl, Screen = screen, RejectionReason = rejectionReason
        };

        // Concerts (incl. Tamil hip-hop)
        var hiphopTamizha = Ev(alice.Id, nehruStadium.Id, "Hiphop Tamizha Live in Concert",
            "Adhi and the Hiphop Tamizha crew bring their high-energy Tamil rap anthems to Chennai for one electrifying night.",
            "Published", "Concerts", "hiphop-tamizha-live-chennai", 21, imgConcert1);
        var anirudhLive = Ev(alice.Id, nehruStadium.Id, "Anirudh Live in Concert",
            "Rockstar Anirudh Ravichander performs his chart-topping hits live with a full band and stunning stage production.",
            "Published", "Concerts", "anirudh-live-chennai", 35, imgConcert2);
        var arivuEmbassy = Ev(bob.Id, codissia.Id, "Arivu & The Embassy",
            "Arivu performs Enjoy Enjaami and his powerful independent Tamil tracks with The Embassy band live in Coimbatore.",
            "Published", "Concerts", "arivu-the-embassy-coimbatore", 28, imgConcert3);
        var santhoshLive = Ev(bob.Id, tamukkam.Id, "Santhosh Narayanan Live",
            "Composer Santhosh Narayanan takes the Madurai stage with a genre-bending live set spanning film and indie work.",
            "Published", "Concerts", "santhosh-narayanan-live-madurai", 42, imgConcert4);
        var yuvanNight = Ev(alice.Id, nehruStadium.Id, "Yuvan Shankar Raja Musical Night",
            "U1 returns to the stage for a nostalgic and electric night of his timeless Tamil melodies and beats.",
            "Published", "Concerts", "yuvan-shankar-raja-night-chennai", 14, imgConcert5);
        var indieFest = Ev(carol.Id, codissia.Id, "Coimbatore Indie Music Fest",
            "A full day of Tamil independent music with rappers, bands and producers from across the state.",
            "Published", "Concerts", "coimbatore-indie-music-fest", 49, imgConcert6);

        // Comedy (stand-up)
        var aravindSA = Ev(carol.Id, annaAuditorium.Id, "Aravind SA: Madrasi Da",
            "Aravind SA returns with his celebrated solo special, a riot of observational comedy on life as a Madrasi.",
            "Published", "Comedy", "aravind-sa-madrasi-da-trichy", 12, imgComedy1);
        var praveenKumar = Ev(carol.Id, tamukkam.Id, "Praveen Kumar Stand-Up",
            "SACT fame Praveen Kumar brings his sharp, relatable Tamil stand-up to Madurai for a laugh-out-loud evening.",
            "Published", "Comedy", "praveen-kumar-standup-madurai", 18, imgComedy2);
        var alexanderBabu = Ev(bob.Id, codissia.Id, "Alexander Babu: Musical Comedy",
            "Alexander Babu blends music and comedy in his signature one-man show packed with songs, stories and laughs.",
            "Published", "Comedy", "alexander-babu-musical-comedy-coimbatore", 25, imgComedy3);
        var rjVignesh = Ev(carol.Id, annaAuditorium.Id, "RJ Vignesh Live",
            "RJ Vignesh takes his viral Tamil humour off the airwaves and onto the stage for a packed live show.",
            "Published", "Comedy", "rj-vignesh-live-trichy", 33, imgComedy1);
        var openMic = Ev(alice.Id, annaAuditorium.Id, "Madras Central Open Mic",
            "The best up-and-coming Tamil comedians test fresh material in a buzzing open-mic night.",
            "Published", "Comedy", "madras-central-open-mic", 9, imgComedy2);

        // Movies (premieres / special screenings)
        var vikramRelease = Ev(alice.Id, sathyamCinema.Id, "Vikram: Re-Release Special",
            "Lokesh Kanagaraj's blockbuster Vikram returns to the big screen in a special fan re-release.",
            "Published", "Movies", "vikram-re-release-special", 7, imgMovie1, screen: "Screen 1");
        var ps2Screening = Ev(bob.Id, sathyamCinema.Id, "Ponniyin Selvan: Part 2 — Special Screening",
            "Experience Mani Ratnam's grand epic Ponniyin Selvan: Part 2 in a premium special screening.",
            "Published", "Movies", "ponniyin-selvan-2-special-screening", 10, imgMovie2, screen: "Screen 2");
        var leoFanShow = Ev(carol.Id, sathyamCinema.Id, "Leo: Fan Celebration Show",
            "A first-day-first-show style fan celebration of Thalapathy Vijay's Leo with the full theatre experience.",
            "Published", "Movies", "leo-fan-celebration-show", 5, imgMovie3, screen: "Screen 3");
        var masterRelease = Ev(alice.Id, annaAuditorium.Id, "Master: Re-Release",
            "Vijay and Vijay Sethupathi's Master is back on the big screen for a limited re-release run.",
            "Published", "Movies", "master-re-release-trichy", 16, imgMovie1, screen: "Audi 1");
        var ninetySix = Ev(bob.Id, sathyamCinema.Id, "96: Re-Release Special",
            "Relive the romance of 96 starring Vijay Sethupathi and Trisha in this special re-release.",
            "Published", "Movies", "96-re-release-special", 20, imgMovie2, screen: "Screen 1");

        // Non-published (dashboard realism)
        var ilaiyaraajaDraft = Ev(alice.Id, nehruStadium.Id, "Ilaiyaraaja 80: Live in Symphony",
            "A symphonic tribute concert celebrating the Maestro Ilaiyaraaja — details being finalised.",
            "Draft", "Concerts", "ilaiyaraaja-80-live-symphony", 60, imgConcert1);
        var hiphopBattle = Ev(bob.Id, codissia.Id, "Chennai Hip-Hop Battle 2026",
            "A statewide Tamil rap and breakdance battle with cash prizes and celebrity judges.",
            "PendingApproval", "Concerts", "chennai-hiphop-battle-2026", 55, imgConcert3);
        var comedyBrawlRejected = Ev(carol.Id, annaAuditorium.Id, "Late Night Comedy Brawl",
            "An after-hours competitive comedy showdown.",
            "Rejected", "Comedy", "late-night-comedy-brawl", 30, imgComedy3,
            rejectionReason: "Insufficient details. Please add performer line-up and run sheet.");
        var maduraiCancelled = Ev(bob.Id, tamukkam.Id, "Madurai Music Marathon",
            "A 12-hour music marathon — cancelled due to venue scheduling conflicts.",
            "Cancelled", "Concerts", "madurai-music-marathon", 45, imgConcert4);

        db.Events.AddRange(
            hiphopTamizha, anirudhLive, arivuEmbassy, santhoshLive, yuvanNight, indieFest,
            aravindSA, praveenKumar, alexanderBabu, rjVignesh, openMic,
            vikramRelease, ps2Screening, leoFanShow, masterRelease, ninetySix,
            ilaiyaraajaDraft, hiphopBattle, comedyBrawlRejected, maduraiCancelled);
        await db.SaveChangesAsync();

        // ── 6. TicketTypes ────────────────────────────────────────────────────
        var saleStart = now.AddDays(-7);

        // Three tiers (Silver/Gold/Premium) per published event; sale ends at event start.
        TicketType[] Tiers(Event ev, decimal silver, decimal gold, decimal premium, int qSilver, int qGold, int qPremium) => new[]
        {
            new TicketType { EventId = ev.Id, Name = "Silver",  SeatType = "Silver",  Price = silver,  TotalQuantity = qSilver,  AvailableQuantity = qSilver,  SaleStart = saleStart, SaleEnd = ev.StartTime, IsActive = true },
            new TicketType { EventId = ev.Id, Name = "Gold",    SeatType = "Gold",    Price = gold,    TotalQuantity = qGold,    AvailableQuantity = qGold,    SaleStart = saleStart, SaleEnd = ev.StartTime, IsActive = true },
            new TicketType { EventId = ev.Id, Name = "Premium", SeatType = "Premium", Price = premium, TotalQuantity = qPremium, AvailableQuantity = qPremium, SaleStart = saleStart, SaleEnd = ev.StartTime, IsActive = true },
        };

        var ttHiphop    = Tiers(hiphopTamizha,  999m, 1999m, 3499m, 40, 30, 20);
        var ttAnirudh   = Tiers(anirudhLive,   1499m, 2999m, 4999m, 40, 30, 20);
        var ttArivu     = Tiers(arivuEmbassy,   799m, 1499m, 2499m, 30, 20, 10);
        var ttSanthosh  = Tiers(santhoshLive,   899m, 1799m, 2999m, 30, 20, 10);
        var ttYuvan     = Tiers(yuvanNight,    1299m, 2499m, 3999m, 40, 30, 20);
        var ttIndie     = Tiers(indieFest,      599m, 1199m, 1999m, 30, 20, 10);
        var ttAravind   = Tiers(aravindSA,      599m,  999m, 1499m, 20, 10,  5);
        var ttPraveen   = Tiers(praveenKumar,   499m,  799m, 1299m, 30, 20, 10);
        var ttAlexander = Tiers(alexanderBabu,  699m, 1099m, 1599m, 30, 20, 10);
        var ttVignesh   = Tiers(rjVignesh,      399m,  699m,  999m, 20, 10,  5);
        var ttOpenMic   = Tiers(openMic,        299m,  499m,  799m, 20, 10,  5);
        var ttVikram    = Tiers(vikramRelease,  150m,  220m,  350m, 40, 30, 20);
        var ttPs2       = Tiers(ps2Screening,   180m,  260m,  400m, 40, 30, 20);
        var ttLeo       = Tiers(leoFanShow,     200m,  300m,  450m, 40, 30, 20);
        var ttMaster    = Tiers(masterRelease,  150m,  220m,  350m, 20, 10,  5);
        var ttNinety    = Tiers(ninetySix,      150m,  220m,  350m, 40, 30, 20);

        db.TicketTypes.AddRange(ttHiphop.Concat(ttAnirudh).Concat(ttArivu).Concat(ttSanthosh)
            .Concat(ttYuvan).Concat(ttIndie).Concat(ttAravind).Concat(ttPraveen).Concat(ttAlexander)
            .Concat(ttVignesh).Concat(ttOpenMic).Concat(ttVikram).Concat(ttPs2).Concat(ttLeo)
            .Concat(ttMaster).Concat(ttNinety));
        await db.SaveChangesAsync();

        // ── 7. Bookings ───────────────────────────────────────────────────────
        // Per-venue seat counters (advance as seats are consumed, so none is reused).
        int nhS = 0, nhG = 0, nhP = 0;
        int syS = 0, syG = 0, syP = 0;
        int cdS = 0, cdG = 0;
        int tmS = 0, tmG = 0, tmP = 0;
        int anS = 0, anG = 0;

        var booking1  = MakeBooking("BK-2026-100001", david.Id, hiphopTamizha.Id, "Confirmed", 1999m, now.AddDays(21));
        var booking2  = MakeBooking("BK-2026-100002", emma.Id,  anirudhLive.Id,   "Confirmed", 4999m, now.AddDays(35));
        var booking3  = MakeBooking("BK-2026-100003", frank.Id, arivuEmbassy.Id,  "Confirmed", 1598m, now.AddDays(28));
        var booking4  = MakeBooking("BK-2026-100004", grace.Id, aravindSA.Id,     "Completed", 999m,  now.AddDays(12), scannedAt: now.AddDays(-1), scannedBy: carol.Id);
        var booking5  = MakeBooking("BK-2026-100005", henry.Id, vikramRelease.Id, "Confirmed", 350m,  now.AddDays(7));
        var booking6  = MakeBooking("BK-2026-100006", david.Id, praveenKumar.Id,  "Pending",   499m,  now.AddHours(1));
        var booking7  = MakeBooking("BK-2026-100007", emma.Id,  indieFest.Id,     "Cancelled", 1199m, now.AddDays(49));
        var booking8  = MakeBooking("BK-2026-100008", frank.Id, anirudhLive.Id,   "Expired",   1499m, now.AddHours(-2));
        var booking9  = MakeBooking("BK-2026-100009", grace.Id, ps2Screening.Id,  "Confirmed", 260m,  now.AddDays(10));
        var booking10 = MakeBooking("BK-2026-100010", henry.Id, santhoshLive.Id,  "Confirmed", 2999m, now.AddDays(42));
        var booking11 = MakeBooking("BK-2026-100011", david.Id, leoFanShow.Id,    "Confirmed", 200m,  now.AddDays(5));
        var booking12 = MakeBooking("BK-2026-100012", emma.Id,  rjVignesh.Id,     "Pending",   399m,  now.AddHours(1));

        db.Bookings.AddRange(booking1, booking2, booking3, booking4, booking5, booking6,
            booking7, booking8, booking9, booking10, booking11, booking12);
        await db.SaveChangesAsync();

        // ── 8. BookingItems ───────────────────────────────────────────────────
        db.BookingItems.AddRange(
            Item(booking1.Id,  ttHiphop[1].Id,   nhGold[nhG++].Id,    1999m, "Sold"),
            Item(booking2.Id,  ttAnirudh[2].Id,  nhPremium[nhP++].Id, 4999m, "Sold"),
            Item(booking3.Id,  ttArivu[0].Id,    cdSilver[cdS++].Id,  799m,  "Sold"),
            Item(booking3.Id,  ttArivu[0].Id,    cdSilver[cdS++].Id,  799m,  "Sold"),
            Item(booking4.Id,  ttAravind[1].Id,  anGold[anG++].Id,    999m,  "Sold"),
            Item(booking5.Id,  ttVikram[2].Id,   syPremium[syP++].Id, 350m,  "Sold"),
            Item(booking6.Id,  ttPraveen[0].Id,  tmSilver[tmS++].Id,  499m,  "Reserved"),
            Item(booking7.Id,  ttIndie[1].Id,    cdGold[cdG++].Id,    1199m, "Cancelled"),
            Item(booking8.Id,  ttAnirudh[0].Id,  nhSilver[nhS++].Id,  1499m, "Cancelled"),
            Item(booking9.Id,  ttPs2[1].Id,      syGold[syG++].Id,    260m,  "Sold"),
            Item(booking10.Id, ttSanthosh[2].Id, tmPremium[tmP++].Id, 2999m, "Sold"),
            Item(booking11.Id, ttLeo[0].Id,      sySilver[syS++].Id,  200m,  "Sold"),
            Item(booking12.Id, ttVignesh[0].Id,  anSilver[anS++].Id,  399m,  "Reserved")
        );
        await db.SaveChangesAsync();

        // ── 9. Payments ───────────────────────────────────────────────────────
        db.Payments.AddRange(
            new Payment { BookingId = booking1.Id,  StripePaymentIntentId = "pi_seed_001", StripeChargeId = "ch_seed_001", StripeCustomerId = "cus_seed_david",  Amount = 1999m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-5) },
            new Payment { BookingId = booking2.Id,  StripePaymentIntentId = "pi_seed_002", StripeChargeId = "ch_seed_002", StripeCustomerId = "cus_seed_emma",   Amount = 4999m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-4) },
            new Payment { BookingId = booking3.Id,  StripePaymentIntentId = "pi_seed_003", StripeChargeId = "ch_seed_003", StripeCustomerId = "cus_seed_frank",  Amount = 1598m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-3) },
            new Payment { BookingId = booking4.Id,  StripePaymentIntentId = "pi_seed_004", StripeChargeId = "ch_seed_004", StripeCustomerId = "cus_seed_grace",  Amount = 999m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-8) },
            new Payment { BookingId = booking5.Id,  StripePaymentIntentId = "pi_seed_005", StripeChargeId = "ch_seed_005", StripeCustomerId = "cus_seed_henry",  Amount = 350m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-2) },
            new Payment { BookingId = booking9.Id,  StripePaymentIntentId = "pi_seed_009", StripeChargeId = "ch_seed_009", StripeCustomerId = "cus_seed_grace2", Amount = 260m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-1) },
            new Payment { BookingId = booking10.Id, StripePaymentIntentId = "pi_seed_010", StripeChargeId = "ch_seed_010", StripeCustomerId = "cus_seed_henry2", Amount = 2999m, Currency = "inr", Status = "Succeeded", PaidAt = now.AddDays(-1) },
            new Payment { BookingId = booking11.Id, StripePaymentIntentId = "pi_seed_011", StripeChargeId = "ch_seed_011", StripeCustomerId = "cus_seed_david2", Amount = 200m,  Currency = "inr", Status = "Succeeded", PaidAt = now.AddHours(-12) }
        );
        await db.SaveChangesAsync();

        // ── 10. SeatReservations ──────────────────────────────────────────────
        db.SeatReservations.AddRange(
            new SeatReservation { SeatId = nhSilver[nhS++].Id,  TicketTypeId = ttHiphop[0].Id,   EventId = hiphopTamizha.Id, UserId = carol.Id, Status = "Active",   ReservedUntil = now.AddMinutes(8) },
            new SeatReservation { SeatId = nhPremium[nhP++].Id, TicketTypeId = ttAnirudh[2].Id,  EventId = anirudhLive.Id,   UserId = henry.Id, Status = "Active",   ReservedUntil = now.AddMinutes(5) },
            new SeatReservation { SeatId = syGold[syG++].Id,    TicketTypeId = ttVikram[1].Id,   EventId = vikramRelease.Id, UserId = david.Id, Status = "Released", ReservedUntil = now.AddMinutes(-5) },
            new SeatReservation { SeatId = anGold[anG++].Id,    TicketTypeId = ttAravind[1].Id,  EventId = aravindSA.Id,     UserId = emma.Id,  Status = "Expired",  ReservedUntil = now.AddMinutes(-15) },
            new SeatReservation { SeatId = tmGold[tmG++].Id,    TicketTypeId = ttSanthosh[1].Id, EventId = santhoshLive.Id,  UserId = frank.Id, Status = "Active",   ReservedUntil = now.AddMinutes(9) }
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
