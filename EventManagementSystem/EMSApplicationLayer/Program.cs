using EMSApplicationLayer;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning.ApiExplorer;
using EMSApplicationLayer.BackgroundServices;
using EMSApplicationLayer.Filters;
using EMSApplicationLayer.Hubs;
using EMSApplicationLayer.Middleware;
using EMSApplicationLayer.Notifications;
using EMSBLLLibrary.Interfaces;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSDALLibrary.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, config) =>
    config.ReadFrom.Configuration(ctx.Configuration));

// ── Stripe ────────────────────────────────────────────────────────────────────
// Secret comes from the environment variable Stripe__SecretKey (double underscore
// maps to the Stripe:SecretKey config key). Never commit the key to appsettings.
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException(
        "Stripe:SecretKey is not configured. Set the Stripe__SecretKey environment variable.");

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── API Versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<EventContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IVenueRepository, VenueRepository>();
builder.Services.AddScoped<ISeatRepository, SeatRepository>();
builder.Services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IBookingItemRepository, BookingItemRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISeatReservationRepository, SeatReservationRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOrganizerRequestRepository, OrganizerRequestRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IVenueService, VenueService>();
builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddScoped<ISeatReservationService, SeatReservationService>();
builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IStripePaymentIntentClient, StripePaymentIntentClient>();
builder.Services.AddScoped<IStripeRefundClient, StripeRefundClient>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddScoped<ISeatNotifier, SignalRSeatNotifier>();

// ── Background Services ───────────────────────────────────────────────────────
builder.Services.AddHostedService<BookingExpiryService>();

// ── AutoMapper ────────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());


// ── Memory Cache (used by IdempotencyFilter) ──────────────────────────────────
builder.Services.AddMemoryCache();

// ── Idempotency filter (registered for IFilterFactory resolution) ─────────────
builder.Services.AddScoped<IdempotencyFilter>();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Strict policy for auth endpoints: 10 requests per minute per IP
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Global limiter: authenticated users get 200 req/min, anonymous get 60 req/min, keyed by user ID or IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = userId ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = userId != null ? 200 : 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"error":"Too many requests. Please slow down and try again."}""", token);
    };
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.ConfigureOptions<EMSApplicationLayer.Swagger.ConfigureSwaggerOptions>();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        foreach (var description in app.DescribeApiVersions())
            c.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"EMS API {description.GroupName.ToUpperInvariant()}");
    });
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SeatHub>("/hubs/seats");

await DataSeeder.SeedAsync(app.Services);

app.Run();
