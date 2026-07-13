using EMSApplicationLayer;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning.ApiExplorer;
using EMSApplicationLayer.BackgroundServices;
using EMSApplicationLayer.Filters;
using EMSApplicationLayer.Helpers;
using EMSApplicationLayer.Hubs;
using EMSApplicationLayer.Middleware;
using EMSApplicationLayer.Notifications;
using EMSBLLLibrary.Emails;
using EMSBLLLibrary.Interfaces;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSDALLibrary.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
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

// Reshape [ApiController] model-validation (DataAnnotation) failures into the same
// { "error": "..." } envelope the ExceptionMiddleware returns, so the frontend has a
// single, consistent error shape to read for every 4xx.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?? "One or more validation errors occurred.";
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = message });
    };
});

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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
builder.Services.AddDbContext<EventContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IScreeningRepository, ScreeningRepository>();
builder.Services.AddScoped<IVenueRepository, VenueRepository>();
builder.Services.AddScoped<ISeatRepository, SeatRepository>();
builder.Services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IBookingItemRepository, BookingItemRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISeatReservationRepository, SeatReservationRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOrganizerRequestRepository, OrganizerRequestRepository>();
builder.Services.AddScoped<IChangeLogRepository, ChangeLogRepository>();
builder.Services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IScreeningService, ScreeningService>();
builder.Services.AddScoped<IVenueService, VenueService>();
builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddScoped<ISeatReservationService, SeatReservationService>();
builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IStripePaymentIntentClient, StripePaymentIntentClient>();
builder.Services.AddScoped<IStripeRefundClient, StripeRefundClient>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
builder.Services.AddScoped<IChangeLogService, ChangeLogService>();

// ── Email ─────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailQueue, EmailQueue>();
builder.Services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddScoped<EventReminderSweeper>();

builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(15);

    var apiKey = config["Email:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
});
// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddScoped<ISeatNotifier, SignalRSeatNotifier>();

// ── Health Checks ─────────────────────────────────────────────────────────────
// "ready" is tagged so the readiness probe can select only the DB check. Liveness
// intentionally has NO checks: a transient DB failure must not cause Kubernetes to restart
// every pod, turning a recoverable blip into an outage.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EventContext>("database", tags: ["ready"]);

// ── Background Services ───────────────────────────────────────────────────────
// These have no leader election: every replica that registers them runs its own copy, so N
// API replicas would send N copies of every email and reminder. In Kubernetes only the
// single-replica `ems-worker` Deployment sets Workers:Enabled; `ems-api` sets it to false.
// Defaults to true so local development is unchanged.
if (builder.Configuration.GetValue("Workers:Enabled", true))
{
    builder.Services.AddHostedService<BookingExpiryService>();
    builder.Services.AddHostedService<EmailDispatcherService>();
    builder.Services.AddHostedService<EventReminderService>();
}

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

builder.Services.AddAuthorization(options =>
{
    // Defense in depth: any endpoint that does not explicitly opt out with
    // [AllowAnonymous] requires an authenticated user. Public browsing endpoints
    // and the auth/webhook/hub surfaces are annotated with [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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
// Explicit allow-list. Origins are read from Cors:AllowedOrigins (array) in
// configuration; falls back to the local Angular dev server when unset. Never
// reflect arbitrary origins together with AllowCredentials.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "https://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
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

// ── Migration mode ────────────────────────────────────────────────────────────
// `--migrate` applies pending EF migrations and exits. Run as a Kubernetes Job that must
// complete before the Deployments roll out, so migrations never race across pods. Kestrel is
// never started and DataSeeder never runs in this mode.
if (args.Contains("--migrate"))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationContext = migrationScope.ServiceProvider.GetRequiredService<EventContext>();
    var migrationLogger = migrationScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    migrationLogger.LogInformation("Applying EF migrations...");
    await migrationContext.Database.MigrateAsync();
    migrationLogger.LogInformation("Migrations applied. Exiting.");
    return;
}

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

// The ingress terminates TLS and forwards plain HTTP to the pod. Trust its headers so the
// app sees the real client scheme and IP — the rate limiter partitions by IP, and without
// this every request would look like it came from the ingress pod and share one bucket.
// KnownNetworks/KnownProxies are cleared because the ingress pod's IP is not known ahead of
// time; the cluster network is not reachable from outside, so this is safe here.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseSerilogRequestLogging();

// Only redirect in development. In the cluster the pod only ever speaks HTTP (TLS is the
// ingress's job), so redirecting here would loop forever AND 307 the kubelet health probes,
// which would crash-loop the pod.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SeatHub>("/hubs/seats");

// Probes are called by kubelet over plain HTTP from inside the cluster. They must be
// anonymous and must not be rate limited, or a throttled probe would get the pod killed.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false   // no checks: a 200 means "the process is running"
}).AllowAnonymous().DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous().DisableRateLimiting();

await DataSeeder.SeedAsync(app.Services);

app.Run();

// Named so ILogger<Program> can be resolved in the --migrate branch above.
public partial class Program { }
