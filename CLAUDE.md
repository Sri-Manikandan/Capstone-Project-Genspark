# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Global Rules

**Git commit messages must be 5 words or fewer.** No body, no bullet points, no co-author lines. Examples: `add event booking flow`, `fix seat reservation bug`, `update auth service`. This applies to every commit in every part of the repo.

## Commands

All commands run from the repo root (`Capstone Project/`). The .NET solution lives under `EventManagementSystem/`.

```bash
# Build entire solution
dotnet build EventManagementSystem/EMS.sln

# Run the API
dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj

# Run all tests
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj

# Run a single test class
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~BookingServiceTests"

# Run a single test method
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~BookingServiceTests.Create_ShouldThrow_WhenEventNotFound"

# EF Core migrations (run from repo root, targeting the DAL project)
dotnet ef migrations add <MigrationName> --project EventManagementSystem/EMSDALLibrary --startup-project EventManagementSystem/EMSApplicationLayer
dotnet ef database update --project EventManagementSystem/EMSDALLibrary --startup-project EventManagementSystem/EMSApplicationLayer
```

## Architecture

Four class-library projects with strict one-way dependency:

```
EMSApplicationLayer  (ASP.NET Core Web API — entry point)
       ↓
EMSBLLLibrary        (business logic services + interfaces)
       ↓
EMSDALLibrary        (EF Core repositories + EventContext)
       ↓
EMSModelLibrary      (domain models, DTOs, custom exceptions)
```

### EMSModelLibrary
- `Models/` — EF Core entities (User, Event, Venue, Seat, TicketType, Booking, BookingItem, Payment, SeatReservation, RefreshToken, OrganizerRequest)
- `DTOs/` — request/response shapes; one file per domain area
- `Exceptions/` — custom hierarchy: `LibraryException` (base) → `NotFoundException`, `ValidationException`, `InvalidCredentialsException`, `UnauthorizedException`, `DatabaseException`

### EMSDALLibrary
- `Contexts/EventContext.cs` — single DbContext; all indexes and FK constraints are configured here via `OnModelCreating`
- `Repositories/AbstractRepository<T>` — generic CRUD base; concrete repositories extend it with domain-specific queries
- `Interfaces/` — one interface per repository; all registered as `Scoped` in DI

### EMSBLLLibrary
- `Services/` — one service per domain; inject only repository interfaces, never `EventContext` directly
- `Interfaces/` — one interface per service; what the controllers depend on
- `Mappings/MappingProfile.cs` — AutoMapper profile; **all datetime fields are converted UTC→IST via `TimeHelper.UtcToIst`** — do not map datetime fields without this conversion
- `Constants/EventStatus.cs`, `OrganizerRequestStatus.cs` — string constants for status fields (statuses are stored as plain strings in the DB)
- `Helpers/InputValidator.cs`, `TimeHelper.cs` — shared validation and timezone utilities

### EMSApplicationLayer
- `Program.cs` — all DI registrations, middleware pipeline, JWT config, rate limiting, CORS, Swagger, SignalR
- `Controllers/` — thin: validate HTTP, call one service method, return result
- `Middleware/ExceptionMiddleware.cs` — catches all custom `LibraryException` subtypes and maps them to HTTP status codes; unhandled exceptions → 500
- `Filters/IdempotencyFilter.cs` — `[Idempotent]` attribute; reads `Idempotency-Key` header and caches successful POST responses for 24 h in `IMemoryCache`
- `Hubs/SeatHub.cs` + `Notifications/SignalRSeatNotifier.cs` — real-time seat availability updates; clients join `event-{id}` groups
- `BackgroundServices/BookingExpiryService.cs` — runs every 60 s; cancels `Pending` bookings past `ExpiresAt`, restores `AvailableQuantity` on TicketTypes, and expires related SeatReservations
- `DataSeeder.cs` — seeds demo data on startup (no-op if any users exist); default password for all seed users is `Test@1234`

## Key Patterns

**Error handling** — throw domain exceptions from BLL/DAL; `ExceptionMiddleware` translates them. Never return raw error strings from services.

**Payment flow** — Stripe PaymentIntent is created via `IStripePaymentIntentClient`; webhook events (payment succeeded/failed/refunded) are handled by `StripeWebhookService` and `StripeWebhookController`. The webhook endpoint skips the global auth middleware.

**Seat reservation** — `SeatReservation` has a partial unique index `WHERE Status = 'Active'` (one active reservation per seat per event). The `BookingExpiryService` cleans up expired reservations. `ISeatNotifier` (implemented by `SignalRSeatNotifier`) broadcasts seat changes to connected clients.

**Roles** — `Admin`, `Organizer`, `User`. Authorization uses `[Authorize(Roles = "...")]` on controller actions. `ClaimsHelper` (in `EventManagementSystem/EMSApplicationLayer/Helpers/`) extracts user ID and role from JWT claims.

**API versioning** — URL-segment style (`/api/v1/...`). Default version is 1.0. Add new versions via `[ApiVersion("2.0")]` on the controller.

## Configuration

`appsettings.json` expects:
- `ConnectionStrings:Default` — PostgreSQL connection string
- `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpiryMinutes`, `Jwt:RefreshTokenExpiryDays`
- `Stripe:SecretKey`, `Stripe:WebhookSecret`

## Tests

`EMSTests` uses **NUnit + Moq + FluentAssertions**. Tests are unit tests: all repositories are mocked with `Moq`; a real `AutoMapper` instance (configured with `MappingProfile`) is used rather than mocking the mapper. Test class names follow `{ServiceName}Tests` and live under `EMSTests/Services/`.

---

## Angular Frontend (EMSAngular/)

The Angular project lives in `EMSAngular/` at the repo root (sibling to `EventManagementSystem/`) and targets the API above. Follow the [Angular style guide](https://angular.dev/style-guide) throughout.

### Angular Commands

```bash
cd EMSAngular

# Install dependencies
npm install

# Start dev server (http://localhost:4200)
ng serve

# Build for production
ng build

# Run unit tests
ng test

# Run a single spec file
ng test --include="**/event.service.spec.ts"

# Generate a component / service / guard / pipe
ng generate component features/events/event-list
ng generate service core/services/auth
ng generate guard core/guards/auth
ng generate pipe shared/pipes/ist-date
```

### Project Structure (LIFT principle — Locate, Identify, Flat, Try DRY)

```
EMSAngular/src/
├── app/
│   ├── core/                  # Singleton services, guards, interceptors, models
│   │   ├── guards/
│   │   ├── interceptors/
│   │   ├── models/            # TypeScript interfaces mirroring API DTOs
│   │   └── services/          # Auth, API services (one file per domain)
│   ├── features/              # Feature areas — one folder per route/domain
│   │   ├── auth/
│   │   ├── events/
│   │   ├── bookings/
│   │   ├── venues/
│   │   └── admin/
│   └── shared/                # Reusable components, directives, pipes used across features
│       ├── components/
│       ├── directives/
│       └── pipes/
└── environments/
```

Organize by **feature, not by type** — avoid flat `components/`, `services/`, `directives/` folders at the app root.

### Naming Conventions

| Artifact | File name | Class name |
|---|---|---|
| Component | `event-list.ts` | `EventListComponent` |
| Service | `auth.service.ts` | `AuthService` |
| Guard | `auth.guard.ts` | `AuthGuard` |
| Interceptor | `jwt.interceptor.ts` | `JwtInterceptor` |
| Pipe | `ist-date.pipe.ts` | `IstDatePipe` |
| Model/Interface | `event.model.ts` | `Event` |
| Spec | `event-list.spec.ts` | — |

- File names: **kebab-case**, words separated by hyphens
- Classes: **PascalCase** with descriptive type suffix (`Component`, `Service`, `Guard`, `Pipe`)
- Component selectors: use a consistent app prefix (e.g., `ems-event-list`)
- Attribute directive selectors: camelCase with prefix (e.g., `[emsHighlight]`)

### Coding Conventions

**Dependency injection** — use the `inject()` function instead of constructor injection:
```ts
// Preferred
export class EventListComponent {
  private eventService = inject(EventService);
}

// Avoid
constructor(private eventService: EventService) {}
```

**Component member order** — Angular-specific members first, then methods:
1. `inject()` calls (dependencies)
2. `input()`, `output()`, `model()` signals
3. `viewChild()` / `contentChild()` queries
4. Other properties
5. Lifecycle hooks (`ngOnInit`, etc.)
6. Methods

**Access modifiers** — use `protected` for members only accessed from the template; `readonly` on `input()`, `output()`, `model()`, and query properties.

**Template logic** — keep templates simple; move complex expressions to `computed()` signals in the component class.

**Event handlers** — name them after the action, not the event (`saveBooking()` not `onButtonClick()`).

**Style bindings** — prefer `[class.active]="..."` and `[style.color]="..."` over `[ngClass]` and `[ngStyle]`.

**One concept per file** — one component/service/pipe per file; split if a file becomes hard to navigate.

**No generic file names** — avoid `helpers.ts`, `utils.ts`, `common.ts`; name files after their specific purpose.

### API Integration

- All HTTP calls go through domain services in `core/services/` (e.g., `EventService`, `BookingService`)
- The `JwtInterceptor` in `core/interceptors/` attaches the `Authorization: Bearer <token>` header
- API base URL is set in `environments/environment.ts` / `environment.prod.ts`
- The backend API runs at `http://localhost:5062` in development (check `EventManagementSystem/EMSApplicationLayer/Properties/launchSettings.json` for the exact port)
- SignalR hub is at `/hubs/seats`; use `@microsoft/signalr` package for real-time seat updates

### Auth

- JWT access token stored in `localStorage`; refresh token flow handled by `AuthService`
- Route protection via `AuthGuard`; role checks (`Admin`, `Organizer`, `User`) via a separate `RoleGuard` or within the same guard
