# Event Management System

A full-stack event management and ticket-booking platform. Customers browse events, reserve seats in real time, and pay via Stripe; organizers create and manage events; admins approve organizers, events, and venues.

- **Backend** — ASP.NET Core 9 Web API (`EventManagementSystem/`)
- **Frontend** — Angular 22 SPA (`EMSAngular/`)
- **Database** — PostgreSQL
- **Realtime** — SignalR (live seat availability)
- **Payments** — Stripe

```
EMSApplicationLayer  →  EMSBLLLibrary  →  EMSDALLibrary  →  EMSModelLibrary
   (Web API)            (services)        (EF Core)         (domain models)
```

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 9.0+ |
| Node.js | 20+ (npm 11+) |
| Angular CLI | `npm install -g @angular/cli` |
| PostgreSQL | 14+ running locally |
| Stripe account | test-mode keys (optional, for payments) |

## 1. Database setup

Create a PostgreSQL database (defaults expected by `appsettings.json`):

- Host: `localhost`  Port: `5432`
- Database: `eventmanagement`
- Username: `postgres`

Update the credentials in `EventManagementSystem/EMSApplicationLayer/appsettings.json` under `ConnectionStrings:Default` to match your local Postgres setup.

## 2. Backend (API)

From the repo root:

```bash
# Restore & build
dotnet build EventManagementSystem/EMS.sln

# Apply EF Core migrations (creates/updates the schema)
dotnet ef database update \
  --project EventManagementSystem/EMSDALLibrary \
  --startup-project EventManagementSystem/EMSApplicationLayer

# Run the API
dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj
```

The API starts at **http://localhost:5222** (HTTPS: https://localhost:7216). Swagger UI is available at `http://localhost:5222/swagger`.

On first run, `DataSeeder` seeds demo data (skipped if any users already exist). The default password for all seeded users is **`Test@1234`**.

### Configuration (`appsettings.json`)

| Key | Purpose |
|---|---|
| `ConnectionStrings:Default` | PostgreSQL connection string |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | JWT signing/validation |
| `Jwt:AccessTokenExpiryMinutes`, `Jwt:RefreshTokenExpiryDays` | Token lifetimes |
| `Stripe:SecretKey`, `Stripe:WebhookSecret` | Stripe payments & webhooks |

> Do not commit real secrets. Use [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or environment variables for production keys.

## 3. Frontend (Angular)

```bash
cd EMSAngular
npm install
ng serve
```

The app runs at **http://localhost:4200** and talks to the API at `http://localhost:5222` (configured in `src/environments/environment.ts`). Make sure the backend is running first.

## Running tests

```bash
# Backend unit tests (NUnit + Moq + FluentAssertions)
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj

# Frontend unit tests
cd EMSAngular && ng test
```

## Stripe webhooks (optional)

To test payment confirmation locally, forward Stripe events to the API webhook endpoint using the Stripe CLI:

```bash
stripe listen --forward-to http://localhost:5222/api/v1/stripe/webhook
```

Copy the printed signing secret into `Stripe:WebhookSecret` in `appsettings.json`.

## Quick start (TL;DR)

```bash
# 1. Start Postgres and update the connection string in appsettings.json
# 2. Backend
dotnet ef database update --project EventManagementSystem/EMSDALLibrary --startup-project EventManagementSystem/EMSApplicationLayer
dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj
# 3. Frontend (new terminal)
cd EMSAngular && npm install && ng serve
# 4. Open http://localhost:4200  (log in with a seeded user / password Test@1234)
```
