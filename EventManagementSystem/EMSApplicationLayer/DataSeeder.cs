using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Contexts;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSApplicationLayer;

public static class DataSeeder
{
    // Runs on startup: seeds only when the database is empty, so a normal boot never
    // touches existing data.
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventContext>();
        if (await db.Users.AnyAsync()) return;
        await PopulateAsync(scope.ServiceProvider);
    }

    // Wipes every seed table and reseeds from scratch. Triggered on demand by the Admin
    // reset endpoint — used to refresh demo data in an already-populated database. Returns
    // row counts of the freshly seeded data.
    public static async Task<Dictionary<string, int>> ResetAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventContext>();

        // TRUNCATE ... CASCADE clears rows and resets identity in one shot, in any order.
        var types = new[]
        {
            typeof(SeatReservation), typeof(Payment), typeof(BookingItem), typeof(Booking),
            typeof(TicketType), typeof(Screening), typeof(Seat), typeof(Event), typeof(Venue),
            typeof(OrganizerRequest), typeof(RefreshToken), typeof(User)
        };
        // Table names come from EF model metadata, never user input, so raw SQL is safe here.
        var tables = types.Select(t => $"\"{db.Model.FindEntityType(t)!.GetTableName()}\"");
        var truncateSql = "TRUNCATE " + string.Join(",", tables) + " RESTART IDENTITY CASCADE;";
        await db.Database.ExecuteSqlRawAsync(truncateSql);

        await PopulateAsync(scope.ServiceProvider);

        return new Dictionary<string, int>
        {
            ["users"] = await db.Users.CountAsync(),
            ["venues"] = await db.Venues.CountAsync(),
            ["events"] = await db.Events.CountAsync(),
            ["screenings"] = await db.Screenings.CountAsync(),
            ["ticketTypes"] = await db.TicketTypes.CountAsync(),
            ["bookings"] = await db.Bookings.CountAsync(),
            ["payments"] = await db.Payments.CountAsync(),
        };
    }

    private static async Task PopulateAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<EventContext>();
        var storage = sp.GetRequiredService<IImageStorage>();
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var now = DateTime.UtcNow;
        var hash = BCrypt.Net.BCrypt.HashPassword("Test@1234");

        // ── 0. Event images ───────────────────────────────────────────────────
        // Committed under SeedAssets/events; streamed through IImageStorage so seeded events
        // reference exactly what a real upload would produce (wwwroot/uploads in dev, the
        // Azure Blob container in prod). Uploaded once, reused across same-theme events.
        var seedDir = Path.Combine(env.ContentRootPath, "SeedAssets", "events");
        var imageKeys = new[]
        {
            "concert-1", "concert-2", "concert-3", "concert-4", "concert-5", "concert-6",
            "comedy-1", "comedy-2", "comedy-3", "movie-1", "movie-2", "movie-3",
            "theatre-1", "theatre-2", "sports-1", "sports-2", "workshop-1", "workshop-2",
            "conference-1", "conference-2", "exhibition-1", "exhibition-2", "festival-1", "festival-2"
        };
        var img = new Dictionary<string, string>();
        foreach (var key in imageKeys)
        {
            await using var fs = File.OpenRead(Path.Combine(seedDir, key + ".jpg"));
            img[key] = await storage.SaveAsync(fs, "image/jpeg");
        }

        // ── 1. Users ──────────────────────────────────────────────────────────
        var admin = new User { Name = "System Admin",  Email = "admin@ems.com", Phone = "9000000001", PasswordHash = hash, Role = "Admin",     IsActive = true };
        var alice = new User { Name = "Alice Johnson", Email = "alice@ems.com", Phone = "9000000002", PasswordHash = hash, Role = "Organizer", IsActive = true };
        var bob   = new User { Name = "Bob Smith",     Email = "bob@ems.com",   Phone = "9000000003", PasswordHash = hash, Role = "Organizer", IsActive = true };
        var carol = new User { Name = "Carol White",   Email = "carol@ems.com", Phone = "9000000004", PasswordHash = hash, Role = "Organizer", IsActive = true };
        var david = new User { Name = "David Brown",   Email = "david@ems.com", Phone = "9000000005", PasswordHash = hash, Role = "User",      IsActive = true };
        var emma  = new User { Name = "Emma Wilson",   Email = "emma@ems.com",  Phone = "9000000006", PasswordHash = hash, Role = "User",      IsActive = true };
        var frank = new User { Name = "Frank Miller",  Email = "frank@ems.com", Phone = "9000000007", PasswordHash = hash, Role = "User",      IsActive = true };
        var grace = new User { Name = "Grace Lee",     Email = "grace@ems.com", Phone = "9000000008", PasswordHash = hash, Role = "User",      IsActive = true };
        var henry = new User { Name = "Henry Chen",    Email = "henry@ems.com", Phone = "9000000009", PasswordHash = hash, Role = "User",      IsActive = true };
        var ivan  = new User { Name = "Ivan Petrov",   Email = "ivan@ems.com",  Phone = "9000000010", PasswordHash = hash, Role = "User",      IsActive = false };

        db.Users.AddRange(admin, alice, bob, carol, david, emma, frank, grace, henry, ivan);
        await db.SaveChangesAsync();

        // ── 2. OrganizerRequests ──────────────────────────────────────────────
        db.OrganizerRequests.AddRange(
            new OrganizerRequest { UserId = alice.Id, Status = "Approved", Reason = "Verified professional event organizer.", RequestedAt = now.AddDays(-30), ReviewedAt = now.AddDays(-29), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = bob.Id,   Status = "Approved", Reason = "Experienced in large-scale events.",     RequestedAt = now.AddDays(-25), ReviewedAt = now.AddDays(-24), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = carol.Id, Status = "Approved", Reason = "Strong portfolio of past events.",       RequestedAt = now.AddDays(-20), ReviewedAt = now.AddDays(-19), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = david.Id, Status = "Pending",  RequestedAt = now.AddDays(-3) },
            new OrganizerRequest { UserId = emma.Id,  Status = "Rejected", Reason = "Portfolio did not meet requirements.",   RequestedAt = now.AddDays(-10), ReviewedAt = now.AddDays(-8), ReviewedByAdminId = admin.Id },
            new OrganizerRequest { UserId = henry.Id, Status = "Pending",  RequestedAt = now.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // ── 3. Venues, screens & seats ────────────────────────────────────────
        // The app models a "screen" as a Seat.Section: a screen holds several seat types, and the
        // seat grid for a screening returns seats WHERE Section == screening.Screen. So every
        // venue is a set of named screens, each carrying the full Silver/Gold/Premium tier set,
        // and a screening's Screen must be one of its venue's screen names.
        //
        // A screen's tiers are laid out with continuously-lettered rows (Silver A.., Gold next..,
        // Premium next..) so (Section, Row, SeatNumber) never repeats within a screen.
        (string type, int rows, int per)[] bigTiers   = { ("Silver", 4, 10), ("Gold", 3, 10), ("Premium", 2, 10) }; // 90 / screen
        (string type, int rows, int per)[] midTiers   = { ("Silver", 3, 10), ("Gold", 2, 10), ("Premium", 1, 10) }; // 60 / screen
        (string type, int rows, int per)[] smallTiers = { ("Silver", 2, 10), ("Gold", 1, 10), ("Premium", 1, 5) };  // 35 / screen

        var nehruStadium       = new Venue { Name = "Nehru Indoor Stadium",        Address = "Sydenhams Road, Periamet",             City = "Chennai",         TotalCapacity = 500 };
        var sathyamCinema      = new Venue { Name = "Sathyam Cinemas",             Address = "8 Thiruvika Road, Royapettah",         City = "Chennai",         TotalCapacity = 400 };
        var codissia           = new Venue { Name = "Codissia Trade Fair Complex", Address = "Trade Fair Road, Peelamedu",           City = "Coimbatore",      TotalCapacity = 300 };
        var tamukkam           = new Venue { Name = "Tamukkam Grounds",            Address = "Tamukkam Road, Aringnar Anna Nagar",   City = "Madurai",         TotalCapacity = 250 };
        var annaAuditorium     = new Venue { Name = "Anna Auditorium",             Address = "Anna Nagar, Thillai Nagar",            City = "Tiruchirappalli", TotalCapacity = 200 };
        var chennaiTradeCentre = new Venue { Name = "Chennai Trade Centre",        Address = "Mount Poonamallee Road, Nandambakkam", City = "Chennai",         TotalCapacity = 600 };
        var kalaignarArangam   = new Venue { Name = "Kalaignar Arangam",           Address = "Avinashi Road, Peelamedu",             City = "Coimbatore",      TotalCapacity = 250 };

        // Each venue's screens. Cinemas/auditoriums have several; other venues run a single "Main".
        var venueScreens = new Dictionary<Venue, (string screen, (string type, int rows, int per)[] tiers)[]>
        {
            [nehruStadium]       = new[] { ("Main", bigTiers) },
            [sathyamCinema]      = new[] { ("Screen 1", bigTiers), ("Screen 2", bigTiers), ("Screen 3", bigTiers) },
            [codissia]           = new[] { ("Main", midTiers) },
            [tamukkam]           = new[] { ("Main", midTiers) },
            [annaAuditorium]     = new[] { ("Audi 1", smallTiers), ("Audi 2", smallTiers) },
            [chennaiTradeCentre] = new[] { ("Main", bigTiers) },
            [kalaignarArangam]   = new[] { ("Main", midTiers) },
        };

        static string LayoutJson((string screen, (string type, int rows, int per)[] tiers)[] screens) =>
            "{\"sections\":[" + string.Join(",", screens.SelectMany(sc => sc.tiers.Select(t =>
                $"{{\"name\":\"{sc.screen}\",\"type\":\"{t.type}\",\"rows\":{t.rows},\"seatsPerRow\":{t.per}}}"))) + "]}";

        foreach (var (venue, screens) in venueScreens)
            venue.LayoutConfig = LayoutJson(screens);

        db.Venues.AddRange(venueScreens.Keys);
        await db.SaveChangesAsync();

        static List<Seat> GenerateVenueSeats(int venueId, (string screen, (string type, int rows, int per)[] tiers)[] screens)
        {
            var seats = new List<Seat>();
            foreach (var (screen, tiers) in screens)
            {
                var rowIdx = 0;
                foreach (var (type, rows, per) in tiers)
                    for (var r = 0; r < rows; r++, rowIdx++)
                    {
                        var row = ((char)('A' + rowIdx)).ToString();
                        for (var n = 1; n <= per; n++)
                            seats.Add(new Seat { VenueId = venueId, Section = screen, Row = row, SeatNumber = n, SeatType = type });
                    }
            }
            return seats;
        }

        foreach (var (venue, screens) in venueScreens)
            db.Seats.AddRange(GenerateVenueSeats(venue.Id, screens));
        await db.SaveChangesAsync();

        // Seats indexed by (venue, screen, type) with a moving cursor so no seat is handed out twice.
        var allSeats = await db.Seats.ToListAsync();
        var seatsByKey = allSeats
            .GroupBy(s => (s.VenueId, s.Section, s.SeatType))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Row).ThenBy(s => s.SeatNumber).ToList());
        var seatCursor = seatsByKey.Keys.ToDictionary(k => k, _ => 0);
        int NextSeatId(int venueId, string screen, string type) =>
            seatsByKey[(venueId, screen, type)][seatCursor[(venueId, screen, type)]++].Id;
        int SeatCount(int venueId, string screen, string type) =>
            seatsByKey.TryGetValue((venueId, screen, type), out var l) ? l.Count : 0;

        string FirstScreen(Venue v) => venueScreens[v][0].screen;
        string[] ScreensOf(Venue v) => venueScreens[v].Select(s => s.screen).ToArray();

        // ── 4. Events ─────────────────────────────────────────────────────────
        // Price is captured per event (Silver/Gold/Premium) so every screening of that event
        // can be given matching ticket tiers.
        var priceMap = new Dictionary<Event, (decimal s, decimal g, decimal p)>();
        var venueOf = new Dictionary<Event, Venue>();
        Event Ev(int organizerId, Venue venue, string title, string description, string status, string category,
                 string slug, int startInDays, string imageKey, decimal s, decimal g, decimal p, string? rejectionReason = null)
        {
            var e = new Event
            {
                OrganizerId = organizerId, VenueId = venue.Id, Title = title, Description = description,
                Status = status, Category = category, Slug = slug,
                StartTime = now.AddDays(startInDays), EndTime = now.AddDays(startInDays).AddHours(3),
                ImageUrl = img[imageKey], Screen = "", RejectionReason = rejectionReason
            };
            priceMap[e] = (s, g, p);
            venueOf[e] = venue;
            return e;
        }

        // Concerts
        var hiphopTamizha     = Ev(alice.Id, nehruStadium,   "Hiphop Tamizha Live in Concert",   "Adhi and the Hiphop Tamizha crew bring their high-energy Tamil rap anthems to Chennai for one electrifying night.", "Published", "Concerts", "hiphop-tamizha-live-chennai", 21, "concert-1",  999m, 1999m, 3499m);
        var anirudhLive       = Ev(alice.Id, nehruStadium,   "Anirudh Live in Concert",          "Rockstar Anirudh Ravichander performs his chart-topping hits live with a full band and stunning stage production.", "Published", "Concerts", "anirudh-live-chennai", 35, "concert-2", 1499m, 2999m, 4999m);
        var arivuEmbassy      = Ev(bob.Id,   codissia,       "Arivu & The Embassy",              "Arivu performs Enjoy Enjaami and his powerful independent Tamil tracks with The Embassy band live in Coimbatore.", "Published", "Concerts", "arivu-the-embassy-coimbatore", 28, "concert-3", 799m, 1499m, 2499m);
        var santhoshLive      = Ev(bob.Id,   tamukkam,       "Santhosh Narayanan Live",          "Composer Santhosh Narayanan takes the Madurai stage with a genre-bending live set spanning film and indie work.", "Published", "Concerts", "santhosh-narayanan-live-madurai", 42, "concert-4", 899m, 1799m, 2999m);
        var yuvanNight        = Ev(alice.Id, nehruStadium,   "Yuvan Shankar Raja Musical Night", "U1 returns to the stage for a nostalgic and electric night of his timeless Tamil melodies and beats.", "Published", "Concerts", "yuvan-shankar-raja-night-chennai", 14, "concert-5", 1299m, 2499m, 3999m);
        var indieFest         = Ev(carol.Id, codissia,       "Coimbatore Indie Music Fest",      "A full day of Tamil independent music with rappers, bands and producers from across the state.", "Published", "Concerts", "coimbatore-indie-music-fest", 49, "concert-6", 599m, 1199m, 1999m);
        var kovaiCarnival     = Ev(carol.Id, codissia,       "Coimbatore Food & Music Carnival", "A weekend carnival pairing Kongu cuisine with live Tamil bands across multiple stages in Coimbatore.", "Published", "Concerts", "coimbatore-food-music-carnival", 22, "concert-5", 699m, 1299m, 1999m);
        var sidSriramKovai    = Ev(bob.Id,   codissia,       "Sid Sriram Live in Coimbatore",    "Sid Sriram brings his soulful voice and hit Tamil melodies to Coimbatore for an intimate live evening.", "Published", "Concerts", "sid-sriram-live-coimbatore", 38, "concert-2", 1299m, 2499m, 3999m);
        var maduraiClassical  = Ev(bob.Id,   tamukkam,       "Madurai Classical Evening",        "An evening of Carnatic classical music by leading artists set against the temple city of Madurai.", "Published", "Concerts", "madurai-classical-evening", 20, "concert-4", 599m, 1099m, 1799m);
        var thaikkudamMadurai = Ev(alice.Id, tamukkam,       "Thaikkudam Bridge Live",           "The multi-genre band Thaikkudam Bridge lights up Madurai with their signature fusion sound.", "Published", "Concerts", "thaikkudam-bridge-live-madurai", 33, "concert-3", 999m, 1899m, 2999m);
        var karthikMadurai    = Ev(bob.Id,   tamukkam,       "Karthik Live in Madurai",          "Playback singer Karthik performs his beloved Tamil chartbusters live with a full band in Madurai.", "Published", "Concerts", "karthik-live-madurai", 44, "concert-6", 1099m, 1999m, 3299m);
        var trichyFiesta      = Ev(carol.Id, annaAuditorium, "Trichy Music Fiesta",              "A vibrant celebration of Tamil music with bands and singers taking the Trichy stage all evening.", "Published", "Concerts", "trichy-music-fiesta", 19, "concert-1", 599m, 999m, 1499m);

        // Comedy
        var aravindSA        = Ev(carol.Id, annaAuditorium,   "Aravind SA: Madrasi Da",        "Aravind SA returns with his celebrated solo special, a riot of observational comedy on life as a Madrasi.", "Published", "Comedy", "aravind-sa-madrasi-da-trichy", 12, "comedy-1", 599m, 999m, 1499m);
        var praveenKumar     = Ev(carol.Id, tamukkam,         "Praveen Kumar Stand-Up",        "SACT fame Praveen Kumar brings his sharp, relatable Tamil stand-up to Madurai for a laugh-out-loud evening.", "Published", "Comedy", "praveen-kumar-standup-madurai", 18, "comedy-2", 499m, 799m, 1299m);
        var alexanderBabu    = Ev(bob.Id,   codissia,         "Alexander Babu: Musical Comedy","Alexander Babu blends music and comedy in his signature one-man show packed with songs, stories and laughs.", "Published", "Comedy", "alexander-babu-musical-comedy-coimbatore", 25, "comedy-3", 699m, 1099m, 1599m);
        var rjVignesh        = Ev(carol.Id, annaAuditorium,   "RJ Vignesh Live",               "RJ Vignesh takes his viral Tamil humour off the airwaves and onto the stage for a packed live show.", "Published", "Comedy", "rj-vignesh-live-trichy", 33, "comedy-1", 399m, 699m, 999m);
        var openMic          = Ev(alice.Id, annaAuditorium,   "Madras Central Open Mic",       "The best up-and-coming Tamil comedians test fresh material in a buzzing open-mic night.", "Published", "Comedy", "madras-central-open-mic", 9, "comedy-2", 299m, 499m, 799m);
        var kovaiComedyNight = Ev(carol.Id, kalaignarArangam, "Kovai Comedy Night",            "A line-up of Tamil stand-up comedians deliver a laugh-packed night in the heart of Coimbatore.", "Published", "Comedy", "kovai-comedy-night", 15, "comedy-2", 499m, 899m, 1399m);
        var maduraiStandUp   = Ev(carol.Id, tamukkam,         "Madurai Stand-Up Special",      "A special showcase of Tamil stand-up talent bringing sharp, local humour to a Madurai audience.", "Published", "Comedy", "madurai-standup-special", 17, "comedy-1", 399m, 699m, 1099m);

        // Movies — hosted at multi-screen venues, so each runs on several screens (see §5).
        var vikramRelease       = Ev(alice.Id, sathyamCinema,  "Vikram: Re-Release Special",                   "Lokesh Kanagaraj's blockbuster Vikram returns to the big screen in a special fan re-release.", "Published", "Movies", "vikram-re-release-special", 7, "movie-1", 150m, 220m, 350m);
        var ps2Screening        = Ev(bob.Id,   sathyamCinema,  "Ponniyin Selvan: Part 2 — Special Screening",  "Experience Mani Ratnam's grand epic Ponniyin Selvan: Part 2 in a premium special screening.", "Published", "Movies", "ponniyin-selvan-2-special-screening", 10, "movie-2", 180m, 260m, 400m);
        var leoFanShow          = Ev(carol.Id, sathyamCinema,  "Leo: Fan Celebration Show",                    "A first-day-first-show style fan celebration of Thalapathy Vijay's Leo with the full theatre experience.", "Published", "Movies", "leo-fan-celebration-show", 5, "movie-3", 200m, 300m, 450m);
        var masterRelease       = Ev(alice.Id, annaAuditorium, "Master: Re-Release",                           "Vijay and Vijay Sethupathi's Master is back on the big screen for a limited re-release run.", "Published", "Movies", "master-re-release-trichy", 16, "movie-1", 150m, 220m, 350m);
        var ninetySix           = Ev(bob.Id,   sathyamCinema,  "96: Re-Release Special",                       "Relive the romance of 96 starring Vijay Sethupathi and Trisha in this special re-release.", "Published", "Movies", "96-re-release-special", 20, "movie-2", 150m, 220m, 350m);
        var trichyMovieMarathon = Ev(alice.Id, annaAuditorium, "Trichy Movie Marathon: Classics",              "A back-to-back big-screen marathon of beloved Tamil classics for a full day of cinema in Trichy.", "Published", "Movies", "trichy-movie-marathon-classics", 24, "movie-3", 150m, 250m, 400m);

        // Theatre
        var chocolateKrishna = Ev(carol.Id, annaAuditorium,   "Crazy Mohan's Chocolate Krishna", "The beloved Tamil comedy play returns to the stage — a hilarious tribute production of Crazy Mohan's classic.", "Published", "Theatre", "crazy-mohan-chocolate-krishna-trichy", 13, "theatre-1", 499m, 899m, 1399m);
        var koothuPattarai   = Ev(bob.Id,   kalaignarArangam, "Koothu-p-Pattarai: Stage Revival","An evening of powerful contemporary Tamil theatre from the Koothu-p-Pattarai repertory company.", "Published", "Theatre", "koothu-p-pattarai-stage-revival-coimbatore", 27, "theatre-2", 399m, 799m, 1299m);

        // Sports
        var tnplNight   = Ev(alice.Id, nehruStadium, "TNPL Exhibition Cricket Night",   "A floodlit exhibition T20 featuring TNPL stars in an action-packed evening of cricket in Chennai.", "Published", "Sports", "tnpl-exhibition-cricket-night-chennai", 11, "sports-1", 299m, 599m, 999m);
        var proKabaddi  = Ev(bob.Id,   nehruStadium, "Chennai Pro Kabaddi Showdown",    "Top Pro Kabaddi franchises clash in a high-intensity showdown at the Nehru Indoor Stadium.", "Published", "Sports", "chennai-pro-kabaddi-showdown", 23, "sports-2", 399m, 799m, 1499m);

        // Workshops
        var aiWorkshop      = Ev(alice.Id, chennaiTradeCentre, "AI & ML Hands-On Workshop",       "A practical full-day workshop on building and deploying machine-learning models, from data to production.", "Published", "Workshops", "ai-ml-hands-on-workshop-chennai", 8, "workshop-1", 999m, 1999m, 2999m);
        var startupBootcamp = Ev(carol.Id, codissia,          "Startup Product Design Bootcamp","A hands-on bootcamp on product design and go-to-market for early-stage founders and product teams.", "Published", "Workshops", "startup-product-design-bootcamp-coimbatore", 26, "workshop-2", 799m, 1499m, 2499m);

        // Conferences
        var tnTechSummit = Ev(alice.Id, chennaiTradeCentre, "Tamil Nadu Tech Summit 2026",  "The state's flagship technology conference with keynotes, panels and startups across two stages.", "Published", "Conferences", "tamil-nadu-tech-summit-2026", 30, "conference-1", 1499m, 2999m, 4999m);
        var fintechConf  = Ev(bob.Id,   codissia,           "Coimbatore FinTech Conference","Leaders in payments, lending and banking tech meet for a day of talks and networking in Coimbatore.", "Published", "Conferences", "coimbatore-fintech-conference", 37, "conference-2", 999m, 1999m, 3499m);

        // Exhibitions
        var bookFair = Ev(carol.Id, chennaiTradeCentre, "Chennai Book Fair Special", "A curated showcase of Tamil and English publishers with author meets, readings and signings.", "Published", "Exhibitions", "chennai-book-fair-special", 6, "exhibition-1", 99m, 199m, 349m);
        var autoExpo = Ev(bob.Id,   codissia,           "Kovai Auto & Tech Expo",    "The latest automobiles, EVs and mobility tech on display across the Codissia halls.", "Published", "Exhibitions", "kovai-auto-tech-expo", 32, "exhibition-2", 149m, 299m, 499m);

        // Festivals
        var chithiraiFestival = Ev(bob.Id,   tamukkam, "Madurai Chithirai Cultural Festival", "A two-day celebration of Madurai's Chithirai heritage with music, dance and cultural performances.", "Published", "Festivals", "madurai-chithirai-cultural-festival", 40, "festival-1", 299m, 599m, 999m);
        var konguFest         = Ev(carol.Id, codissia, "Kongu Food & Music Festival",         "A vibrant festival of Kongu cuisine and live Tamil music across a full weekend in Coimbatore.", "Published", "Festivals", "kongu-food-music-festival-coimbatore", 34, "festival-2", 399m, 799m, 1299m);

        // Other
        var communityMeetup = Ev(alice.Id, chennaiTradeCentre, "EventHub Community Meetup", "An informal meetup for organizers and event enthusiasts to swap ideas over talks and coffee.", "Published", "Other", "eventhub-community-meetup-chennai", 9, "workshop-2", 199m, 399m, 599m);

        // Non-published (dashboard realism — every event status represented).
        var ilaiyaraajaDraft    = Ev(alice.Id, nehruStadium,   "Ilaiyaraaja 80: Live in Symphony", "A symphonic tribute concert celebrating the Maestro Ilaiyaraaja — details being finalised.", "Draft", "Concerts", "ilaiyaraaja-80-live-symphony", 60, "concert-1", 1499m, 2999m, 4999m);
        var hiphopBattle        = Ev(bob.Id,   codissia,       "Chennai Hip-Hop Battle 2026",      "A statewide Tamil rap and breakdance battle with cash prizes and celebrity judges.", "PendingApproval", "Concerts", "chennai-hiphop-battle-2026", 55, "concert-3", 499m, 999m, 1499m);
        var comedyBrawlRejected = Ev(carol.Id, annaAuditorium, "Late Night Comedy Brawl",          "An after-hours competitive comedy showdown.", "Rejected", "Comedy", "late-night-comedy-brawl", 30, "comedy-3", 399m, 699m, 999m, rejectionReason: "Insufficient details. Please add performer line-up and run sheet.");
        var maduraiCancelled    = Ev(bob.Id,   tamukkam,       "Madurai Music Marathon",           "A 12-hour music marathon — cancelled due to venue scheduling conflicts.", "Cancelled", "Concerts", "madurai-music-marathon", 45, "concert-4", 599m, 1099m, 1799m);

        var allEvents = new[]
        {
            hiphopTamizha, anirudhLive, arivuEmbassy, santhoshLive, yuvanNight, indieFest,
            kovaiCarnival, sidSriramKovai, maduraiClassical, thaikkudamMadurai, karthikMadurai, trichyFiesta,
            aravindSA, praveenKumar, alexanderBabu, rjVignesh, openMic, kovaiComedyNight, maduraiStandUp,
            vikramRelease, ps2Screening, leoFanShow, masterRelease, ninetySix, trichyMovieMarathon,
            chocolateKrishna, koothuPattarai, tnplNight, proKabaddi, aiWorkshop, startupBootcamp,
            tnTechSummit, fintechConf, bookFair, autoExpo, chithiraiFestival, konguFest, communityMeetup,
            ilaiyaraajaDraft, hiphopBattle, comedyBrawlRejected, maduraiCancelled
        };
        db.Events.AddRange(allEvents);
        await db.SaveChangesAsync();

        // ── 5. Screenings ─────────────────────────────────────────────────────
        // Movies run on each of their venue's screens; a couple of events run two showtimes on
        // the venue's first screen; the rest have a single screening. A screening's Screen is
        // always a real section of its venue, so the seat grid resolves.
        var twoShowEvents = new HashSet<Event> { aravindSA, chithiraiFestival };

        var primary = new Dictionary<int, Screening>();
        var screeningsByEvent = new Dictionary<int, List<Screening>>();
        foreach (var ev in allEvents)
        {
            var venue = venueOf[ev];
            var list = new List<Screening>();
            if (ev.Category == "Movies")
            {
                var screens = ScreensOf(venue);
                for (var i = 0; i < screens.Length; i++)
                {
                    var start = ev.StartTime.AddHours(i * 3);
                    list.Add(new Screening { EventId = ev.Id, Screen = screens[i], StartTime = start, EndTime = start.AddHours(3), Status = "Scheduled" });
                }
            }
            else if (twoShowEvents.Contains(ev))
            {
                // Festivals span days; other two-show events run twice the same evening.
                var gapHours = ev.Category == "Festivals" ? 24 : 3;
                var screen = FirstScreen(venue);
                list.Add(new Screening { EventId = ev.Id, Screen = screen, StartTime = ev.StartTime, EndTime = ev.EndTime, Status = "Scheduled" });
                var start2 = ev.StartTime.AddHours(gapHours);
                list.Add(new Screening { EventId = ev.Id, Screen = screen, StartTime = start2, EndTime = start2.AddHours(3), Status = "Scheduled" });
            }
            else
            {
                list.Add(new Screening { EventId = ev.Id, Screen = FirstScreen(venue), StartTime = ev.StartTime, EndTime = ev.EndTime, Status = "Scheduled" });
            }
            db.Screenings.AddRange(list);
            primary[ev.Id] = list[0];
            screeningsByEvent[ev.Id] = list;
        }
        await db.SaveChangesAsync();

        // ── 6. TicketTypes ────────────────────────────────────────────────────
        // One Silver/Gold/Premium tier per screening, quantity = the seat count for that type on
        // that screen. Sale runs from a week ago until the screening starts.
        var saleStart = now.AddDays(-7);
        TicketType[] TiersFor(Screening sc, int venueId, decimal silver, decimal gold, decimal premium)
        {
            var tiers = new List<TicketType>();
            void AddTier(string type, decimal price)
            {
                var qty = SeatCount(venueId, sc.Screen, type);
                if (qty > 0)
                    tiers.Add(new TicketType
                    {
                        ScreeningId = sc.Id, Name = type, SeatType = type, Price = price,
                        TotalQuantity = qty, AvailableQuantity = qty,
                        SaleStart = saleStart, SaleEnd = sc.StartTime, IsActive = true
                    });
            }
            AddTier("Silver", silver);
            AddTier("Gold", gold);
            AddTier("Premium", premium);
            return tiers.ToArray();
        }

        // tiersByScreening[screeningId] = [Silver, Gold, Premium] (every screen has all three).
        var tiersByScreening = new Dictionary<int, TicketType[]>();
        foreach (var ev in allEvents)
        {
            var (ps, pg, pp) = priceMap[ev];
            foreach (var sc in screeningsByEvent[ev.Id])
            {
                var tiers = TiersFor(sc, ev.VenueId, ps, pg, pp);
                db.TicketTypes.AddRange(tiers);
                tiersByScreening[sc.Id] = tiers;
            }
        }
        await db.SaveChangesAsync();

        // ── 7. Bookings + items ───────────────────────────────────────────────
        // AddBooking wires a booking to a specific screening (scrIndex), picks the right tier and
        // a fresh seat on that screen, and derives the total from the tier price.
        var byRef = new Dictionary<string, Booking>();
        var pendingItems = new List<(Booking booking, TicketType tier, int venueId, string screen, decimal unit, int qty, string itemStatus)>();

        Booking AddBooking(string reference, User user, Event ev, int scrIndex, string status, int tierIndex, int qty,
            DateTime expiresAt, DateTime? scannedAt = null, User? scannedBy = null)
        {
            var sc = screeningsByEvent[ev.Id][scrIndex];
            var tier = tiersByScreening[sc.Id][tierIndex];
            var itemStatus = status switch
            {
                "Confirmed" or "Completed" => "Sold",
                "Pending" => "Reserved",
                _ => "Cancelled"
            };
            var payload = "{\"ref\":\"" + reference + "\",\"screeningId\":" + sc.Id + ",\"userId\":" + user.Id + "}";
            var booking = new Booking
            {
                BookingReference = reference,
                QrCode = QrCodeHelper.GeneratePngBase64(payload),
                QrPayload = payload,
                UserId = user.Id,
                ScreeningId = sc.Id,
                BookingStatus = status,
                TotalAmount = tier.Price * qty,
                ExpiresAt = expiresAt,
                ScannedAt = scannedAt,
                ScannedBy = scannedBy?.Id
            };
            db.Bookings.Add(booking);
            byRef[reference] = booking;
            pendingItems.Add((booking, tier, ev.VenueId, sc.Screen, tier.Price, qty, itemStatus));
            return booking;
        }

        AddBooking("BK-2026-100001", david, hiphopTamizha,     0, "Confirmed", 1, 1, now.AddDays(21));
        AddBooking("BK-2026-100002", emma,  anirudhLive,       0, "Confirmed", 2, 1, now.AddDays(35));
        AddBooking("BK-2026-100003", frank, arivuEmbassy,      0, "Confirmed", 0, 2, now.AddDays(28));
        AddBooking("BK-2026-100004", grace, aravindSA,         0, "Completed", 1, 1, now.AddDays(12), scannedAt: now.AddDays(-1), scannedBy: carol);
        AddBooking("BK-2026-100005", henry, vikramRelease,     0, "Confirmed", 2, 1, now.AddDays(7));
        AddBooking("BK-2026-100006", david, praveenKumar,      0, "Pending",   0, 1, now.AddHours(1));
        AddBooking("BK-2026-100007", emma,  indieFest,         0, "Cancelled", 1, 1, now.AddDays(49));
        AddBooking("BK-2026-100008", frank, anirudhLive,       0, "Expired",   0, 1, now.AddHours(-2));
        AddBooking("BK-2026-100009", grace, ps2Screening,      0, "Confirmed", 1, 1, now.AddDays(10));
        AddBooking("BK-2026-100010", henry, santhoshLive,      0, "Confirmed", 2, 1, now.AddDays(42));
        AddBooking("BK-2026-100011", david, leoFanShow,        1, "Confirmed", 0, 1, now.AddDays(5));  // secondary screen
        AddBooking("BK-2026-100012", emma,  rjVignesh,         0, "Pending",   0, 1, now.AddHours(1));
        AddBooking("BK-2026-100013", grace, tnTechSummit,      0, "Confirmed", 1, 1, now.AddDays(30));
        AddBooking("BK-2026-100014", henry, aiWorkshop,        0, "Completed", 0, 1, now.AddDays(8), scannedAt: now.AddDays(-2), scannedBy: alice);
        AddBooking("BK-2026-100015", david, chithiraiFestival, 0, "Confirmed", 1, 1, now.AddDays(40));
        AddBooking("BK-2026-100016", frank, vikramRelease,     1, "Confirmed", 1, 1, now.AddDays(7));  // secondary screen, same movie as 100005
        AddBooking("BK-2026-100017", emma,  proKabaddi,        0, "Confirmed", 2, 1, now.AddDays(23));
        AddBooking("BK-2026-100018", grace, bookFair,          0, "Cancelled", 0, 1, now.AddDays(6));
        await db.SaveChangesAsync();

        foreach (var (booking, tier, venueId, screen, unit, qty, itemStatus) in pendingItems)
            for (var i = 0; i < qty; i++)
                db.BookingItems.Add(Item(booking.Id, tier.Id, NextSeatId(venueId, screen, tier.SeatType), unit, itemStatus));
        await db.SaveChangesAsync();

        // ── 8. Payments ───────────────────────────────────────────────────────
        // Succeeded for paid bookings; one Failed (payment attempt on an expired hold) and one
        // Refunded (a cancelled-and-refunded booking) so every payment status is represented.
        Payment Pay(string reference, string status, DateTime? paidAt)
        {
            var b = byRef[reference];
            return new Payment
            {
                BookingId = b.Id,
                StripePaymentIntentId = "pi_seed_" + reference.Replace("BK-2026-", ""),
                StripeChargeId = "ch_seed_" + reference.Replace("BK-2026-", ""),
                StripeCustomerId = "cus_seed_" + b.UserId,
                Amount = b.TotalAmount,
                Currency = "inr",
                Status = status,
                PaidAt = paidAt
            };
        }
        db.Payments.AddRange(
            Pay("BK-2026-100001", "Succeeded", now.AddDays(-5)),
            Pay("BK-2026-100002", "Succeeded", now.AddDays(-4)),
            Pay("BK-2026-100003", "Succeeded", now.AddDays(-3)),
            Pay("BK-2026-100004", "Succeeded", now.AddDays(-8)),
            Pay("BK-2026-100005", "Succeeded", now.AddDays(-2)),
            Pay("BK-2026-100009", "Succeeded", now.AddDays(-1)),
            Pay("BK-2026-100010", "Succeeded", now.AddDays(-1)),
            Pay("BK-2026-100011", "Succeeded", now.AddHours(-12)),
            Pay("BK-2026-100013", "Succeeded", now.AddDays(-2)),
            Pay("BK-2026-100014", "Succeeded", now.AddDays(-6)),
            Pay("BK-2026-100015", "Succeeded", now.AddDays(-1)),
            Pay("BK-2026-100016", "Succeeded", now.AddHours(-20)),
            Pay("BK-2026-100017", "Succeeded", now.AddDays(-1)),
            Pay("BK-2026-100008", "Failed", null),
            Pay("BK-2026-100018", "Refunded", now.AddDays(-4))
        );
        await db.SaveChangesAsync();

        // ── 9. SeatReservations ───────────────────────────────────────────────
        // Short-lived holds a user has placed while choosing seats. Active holds sit in the
        // future; Released/Expired are past. Each takes a fresh seat on the screening's screen.
        SeatReservation Hold(Event ev, int tierIndex, User user, string status, DateTime until)
        {
            var sc = primary[ev.Id];
            var tier = tiersByScreening[sc.Id][tierIndex];
            return new SeatReservation
            {
                SeatId = NextSeatId(ev.VenueId, sc.Screen, tier.SeatType),
                TicketTypeId = tier.Id,
                ScreeningId = sc.Id,
                UserId = user.Id,
                Status = status,
                ReservedUntil = until
            };
        }
        db.SeatReservations.AddRange(
            Hold(hiphopTamizha, 0, carol, "Active",   now.AddMinutes(8)),
            Hold(anirudhLive,   2, henry, "Active",   now.AddMinutes(5)),
            Hold(vikramRelease, 1, david, "Released", now.AddMinutes(-5)),
            Hold(aravindSA,     1, emma,  "Expired",  now.AddMinutes(-15)),
            Hold(santhoshLive,  1, frank, "Active",   now.AddMinutes(9))
        );
        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static BookingItem Item(int bookingId, int ticketTypeId, int seatId, decimal price, string status) => new()
    {
        BookingId = bookingId,
        TicketTypeId = ticketTypeId,
        SeatId = seatId,
        UnitPrice = price,
        TicketStatus = status
    };
}
