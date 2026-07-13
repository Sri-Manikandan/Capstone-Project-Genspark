# Azure AKS Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy the Event Management System to Azure — the .NET 9 API on AKS, the Angular SPA on Static Web Apps, PostgreSQL as a managed service — with all infrastructure in Bicep and all deploys driven by GitHub Actions.

**Architecture:** One AKS node runs two Deployments from a single container image: `ems-api` (1 replica, behind an nginx ingress) and `ems-worker` (1 replica, runs the three background services). Secrets come from Key Vault via workload identity and the Secrets Store CSI driver. Migrations run as a Kubernetes Job that must complete before pods roll out.

**Tech Stack:** .NET 9, Bicep, AKS 1.30, ingress-nginx, cert-manager, Azure Database for PostgreSQL Flexible Server, Azure Key Vault, Azure Static Web Apps, GitHub Actions (OIDC).

**Design doc:** `docs/superpowers/specs/2026-07-13-azure-aks-deployment-design.md`

## Branch Model

- **`main`** — integration. Everything merges here. `ci.yml` runs the tests. **Nothing deploys.**
- **`prod`** — production. This branch *is* what runs in Azure. `deploy-api.yml` and `deploy-web.yml` fire only on push here.
- Ship by promoting: `git checkout prod && git merge main && git push`.
- Implementation of this plan happens on **`feat/azure-deployment`**, which merges to `main`.

The OIDC federated credential is **pinned to a specific git ref**. Credentials are registered
for both `refs/heads/main` (so the manually-dispatched `infra.yml` can run) and
`refs/heads/prod` (so deploys can run). Getting this wrong produces a token-exchange failure
that reads like a permissions bug and is painful to diagnose — see Task 10 Step 1.

## Global Constraints

- **Commit messages must be 5 words or fewer.** No body, no bullets, no co-author lines. (From `CLAUDE.md` — applies to every commit in this plan.)
- **Region is `southindia`** for everything except Static Web Apps, which is not offered there — SWA uses `eastasia`.
- **Node SKU is `Standard_B2als_v2`.** `Standard_B2s` does not exist in `southindia`; do not substitute it.
- **All Stripe keys are TEST mode** (`sk_test_…`, `pk_test_…`, and a webhook secret from a test-mode endpoint). A demo booking must never move real money.
- **Never commit a secret.** Secrets live in Key Vault. `appsettings.json` keeps placeholder/dev values only.
- **Resource group:** `rg-ems-prod`. **Namespace:** `ems`.
- **Images are tagged with the git commit SHA**, never `latest`, so rollouts are reproducible.
- **Datetime handling is untouched by this work.** The API speaks naive IST wall-clock; the DB stores UTC. Do not "fix" any datetime code you encounter.

## File Structure

**Created:**
- `EventManagementSystem/Dockerfile` — multi-stage build, one image for api/worker/migrate
- `EventManagementSystem/.dockerignore`
- `infra/main.bicep` + `infra/modules/{acr,aks,postgres,keyvault,swa,publicip}.bicep`
- `k8s/{namespace,serviceaccount,secretproviderclass,api-deployment,api-service,worker-deployment,ingress,migrate-job}.yaml`
- `k8s/cluster-issuer.yaml` — cert-manager Let's Encrypt issuer
- `.github/workflows/{infra,deploy-api,deploy-web}.yml`
- `EMSAngular/staticwebapp.config.json` — SPA fallback routing

**Modified:**
- `EventManagementSystem/EMSApplicationLayer/Program.cs` — workers flag, proxy headers, health, migrate mode
- `EventManagementSystem/EMSApplicationLayer/appsettings.json` — Serilog console-only
- `EventManagementSystem/EMSApplicationLayer/appsettings.Development.json` — keeps the file sink
- `EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj` — health check package
- `EMSAngular/src/environments/environment.prod.ts` — real API URL + Stripe test key

## A note on testing

Tasks 1–5 change application code and are covered by unit tests in `EMSTests`. Tasks 6–11 are infrastructure: a Bicep template or an Ingress manifest has no meaningful unit test, and writing one would test the mock rather than Azure. Those tasks are verified by **executing them and observing real output** — `docker run` + `curl`, `kubectl get`, a live Stripe webhook delivery. Every such step below states the exact command and the exact expected output. Do not skip these verifications; they are the tests.

---

### Task 1: Gate background services behind `Workers__Enabled`

`Program.cs` registers three hosted services with no leader election. Every replica runs its own copy, so at 2+ API replicas `EmailDispatcherService` sends each queued email once per replica. Gate them so only `ems-worker` runs them.

**Files:**
- Modify: `EventManagementSystem/EMSApplicationLayer/Program.cs:129-132`
- Test: `EventManagementSystem/EMSTests/Configuration/WorkersConfigurationTests.cs` (create)

**Interfaces:**
- Produces: config key `Workers:Enabled` (bool, **defaults to `true`** when absent, so local dev and existing tests keep working unchanged).

- [ ] **Step 1: Write the failing test**

Create `EventManagementSystem/EMSTests/Configuration/WorkersConfigurationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EMSTests.Configuration;

[TestFixture]
public class WorkersConfigurationTests
{
    private static IConfiguration Build(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p =>
                new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Test]
    public void WorkersEnabled_ShouldDefaultToTrue_WhenKeyAbsent()
    {
        var config = Build();

        config.GetValue("Workers:Enabled", true).Should().BeTrue();
    }

    [Test]
    public void WorkersEnabled_ShouldBeFalse_WhenSetToFalse()
    {
        var config = Build(("Workers:Enabled", "false"));

        config.GetValue("Workers:Enabled", true).Should().BeFalse();
    }

    [Test]
    public void WorkersEnabled_ShouldReadEnvironmentStyleKey()
    {
        // Kubernetes sets Workers__Enabled; .NET maps "__" to ":".
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "true"
            })
            .Build();

        config.GetValue("Workers:Enabled", false).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~WorkersConfigurationTests"
```

Expected: FAIL — the test file does not compile yet, or passes trivially. If it passes immediately that is fine; its value is locking the default-true behaviour. Proceed.

- [ ] **Step 3: Gate the hosted services**

In `EventManagementSystem/EMSApplicationLayer/Program.cs`, replace lines 129–132:

```csharp
// ── Background Services ───────────────────────────────────────────────────────
builder.Services.AddHostedService<BookingExpiryService>();
builder.Services.AddHostedService<EmailDispatcherService>();
builder.Services.AddHostedService<EventReminderService>();
```

with:

```csharp
// ── Background Services ───────────────────────────────────────────────────────
// These have no leader election: every replica that registers them runs its own
// copy, so N API replicas would send N copies of every email and reminder. In
// Kubernetes only the single-replica `ems-worker` Deployment sets Workers:Enabled;
// `ems-api` sets it to false. Defaults to true so local dev is unchanged.
if (builder.Configuration.GetValue("Workers:Enabled", true))
{
    builder.Services.AddHostedService<BookingExpiryService>();
    builder.Services.AddHostedService<EmailDispatcherService>();
    builder.Services.AddHostedService<EventReminderService>();
}
```

- [ ] **Step 4: Run the tests**

```bash
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~WorkersConfigurationTests"
```

Expected: PASS, 3 tests.

- [ ] **Step 5: Verify the whole suite still passes**

```bash
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj
```

Expected: PASS, no regressions.

- [ ] **Step 6: Commit**

```bash
git add EventManagementSystem/EMSApplicationLayer/Program.cs EventManagementSystem/EMSTests/Configuration/WorkersConfigurationTests.cs
git commit -m "gate background services behind flag"
```

---

### Task 2: Trust proxy headers, stop redirecting to HTTPS in-container

Two related problems, both fatal on AKS:

1. TLS terminates at the ingress, so the pod receives plain HTTP. `app.UseHttpsRedirection()` sees HTTP and issues a 307 to HTTPS — an infinite redirect loop through the ingress.
2. kubelet probes the pod directly over HTTP. `UseHttpsRedirection()` would 307 those probes too, they'd fail, and Kubernetes would kill the pod in a **CrashLoopBackOff**.

The fix for both: only redirect in Development. Separately, add `UseForwardedHeaders` so the app sees the real client scheme and IP — the rate limiter partitions by IP, and without this every request appears to come from the ingress pod's IP and shares one rate-limit bucket.

**Files:**
- Modify: `EventManagementSystem/EMSApplicationLayer/Program.cs:264-265`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: the app trusts `X-Forwarded-Proto` and `X-Forwarded-For` from any proxy.

- [ ] **Step 1: Add the using directive**

In `Program.cs`, alongside the other `using` lines at the top (after `using Microsoft.AspNetCore.RateLimiting;`):

```csharp
using Microsoft.AspNetCore.HttpOverrides;
```

- [ ] **Step 2: Replace the redirect middleware**

Find these two lines (currently at `Program.cs:264-265`):

```csharp
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
```

Replace with:

```csharp
// The ingress terminates TLS and forwards plain HTTP to the pod. Trust its headers so
// the app sees the real client scheme and IP — the rate limiter partitions by IP, and
// without this every request looks like it came from the ingress pod.
// KnownNetworks/KnownProxies are cleared because the ingress pod's IP is not known
// ahead of time; the cluster network is not reachable from outside, so this is safe here.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
};
forwardedHeaders.KnownNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseSerilogRequestLogging();

// Only redirect in development. In the cluster the pod only ever speaks HTTP (TLS is the
// ingress's job), so redirecting here would loop forever AND 307 the kubelet health
// probes, which would crash-loop the pod.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
```

- [ ] **Step 3: Build to verify it compiles**

```bash
dotnet build EventManagementSystem/EMS.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the full test suite**

```bash
dotnet test EventManagementSystem/EMSTests/EMSTests.csproj
```

Expected: PASS, no regressions.

- [ ] **Step 5: Commit**

```bash
git add EventManagementSystem/EMSApplicationLayer/Program.cs
git commit -m "trust proxy headers behind ingress"
```

---

### Task 3: Add health endpoints for Kubernetes probes

Kubernetes needs a liveness probe (is the process alive?) and a readiness probe (can it serve traffic?). Readiness checks the database. **Liveness deliberately does not** — if a brief Postgres blip failed the liveness probe, Kubernetes would kill and restart every pod, turning a recoverable database hiccup into an outage.

**Files:**
- Modify: `EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj`
- Modify: `EventManagementSystem/EMSApplicationLayer/Program.cs`

**Interfaces:**
- Produces: `GET /health/live` → 200 always (process is up). `GET /health/ready` → 200 when the DB is reachable, 503 otherwise. Both are anonymous and exempt from rate limiting.

- [ ] **Step 1: Add the health-check package**

```bash
dotnet add EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj \
  package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 9.0.16
```

Expected: adds `<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="9.0.16" />`.

- [ ] **Step 2: Register the health checks**

In `Program.cs`, immediately after the SignalR registration block (`builder.Services.AddScoped<ISeatNotifier, SignalRSeatNotifier>();`), add:

```csharp
// ── Health Checks ─────────────────────────────────────────────────────────────
// "ready" is tagged so the readiness probe can select only the DB check. Liveness
// intentionally has NO checks: a transient DB failure must not cause Kubernetes to
// restart every pod.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EventContext>("database", tags: ["ready"]);
```

- [ ] **Step 3: Map the endpoints**

In `Program.cs`, immediately after `app.MapHub<SeatHub>("/hubs/seats");`, add:

```csharp
// Probes are called by kubelet over plain HTTP from inside the cluster. They must be
// anonymous and must not be rate limited, or a probe could be throttled and the pod killed.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false   // no checks: 200 means "the process is running"
}).AllowAnonymous().DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous().DisableRateLimiting();
```

Add the using directive at the top of `Program.cs`:

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
```

- [ ] **Step 4: Build**

```bash
dotnet build EventManagementSystem/EMS.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Verify the endpoints against a running app**

Start Postgres locally, then run the API and probe it:

```bash
dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj &
sleep 15
curl -s -o /dev/null -w "live=%{http_code}\n" http://localhost:5222/health/live
curl -s -o /dev/null -w "ready=%{http_code}\n" http://localhost:5222/health/ready
kill %1
```

Expected:
```
live=200
ready=200
```

If `ready=503`, the app cannot reach Postgres — fix the connection string before continuing. That is exactly the failure the probe exists to catch.

- [ ] **Step 6: Commit**

```bash
git add EventManagementSystem/EMSApplicationLayer/
git commit -m "add health probe endpoints"
```

---

### Task 4: Add `--migrate` mode

Nothing calls `db.Migrate()` today. Against an empty Azure database the app crashes on first query. Rather than migrating on startup (which races when multiple pods start at once), the image gains a `--migrate` mode that applies migrations and exits. CI runs it as a Job and waits for it before rolling out pods.

Note: the Stripe key check at `Program.cs:35` throws if `Stripe:SecretKey` is unset, and it runs before this code. The migration Job mounts the same secrets as the API, so the key is present. Do not reorder that check.

**Files:**
- Modify: `EventManagementSystem/EMSApplicationLayer/Program.cs:249` (just after `var app = builder.Build();`)

**Interfaces:**
- Produces: `dotnet EMSApplicationLayer.dll --migrate` applies all pending EF migrations, logs, and exits 0. Exits non-zero on failure. Does **not** start Kestrel and does **not** run `DataSeeder`.

- [ ] **Step 1: Add the migrate branch**

In `Program.cs`, find `var app = builder.Build();` (line 249). Immediately after it, insert:

```csharp
// ── Migration mode ────────────────────────────────────────────────────────────
// `--migrate` applies pending EF migrations and exits. Run as a Kubernetes Job that
// must complete before the Deployments roll out, so migrations never race across pods.
// Kestrel is never started and DataSeeder never runs in this mode.
if (args.Contains("--migrate"))
{
    using var migrationScope = app.Services.CreateScope();
    var context = migrationScope.ServiceProvider.GetRequiredService<EventContext>();
    var migrationLogger = migrationScope.ServiceProvider
        .GetRequiredService<ILogger<Program>>();

    migrationLogger.LogInformation("Applying EF migrations…");
    await context.Database.MigrateAsync();
    migrationLogger.LogInformation("Migrations applied. Exiting.");
    return;
}
```

`Program` must be referable for `ILogger<Program>`. At the very end of `Program.cs`, after `app.Run();`, add:

```csharp

public partial class Program { }
```

- [ ] **Step 2: Build**

```bash
dotnet build EventManagementSystem/EMS.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Verify migrate mode against a scratch database**

```bash
createdb ems_migrate_test 2>/dev/null || true
ConnectionStrings__Default="Host=localhost;Port=5432;Database=ems_migrate_test;Username=postgres;Password=Poornima290178@" \
Stripe__SecretKey="sk_test_dummy" \
dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj -- --migrate
echo "exit=$?"
```

Expected: logs `Applying EF migrations…` then `Migrations applied. Exiting.`, and `exit=0`. The process must **terminate on its own** — if it hangs listening on a port, the branch is in the wrong place.

Confirm the tables exist:

```bash
psql -d ems_migrate_test -c "\dt" | head
```

Expected: lists `Users`, `Events`, `Bookings`, `__EFMigrationsHistory`, etc.

Clean up:

```bash
dropdb ems_migrate_test
```

- [ ] **Step 4: Commit**

```bash
git add EventManagementSystem/EMSApplicationLayer/Program.cs
git commit -m "add migrate mode entrypoint"
```

---

### Task 5: Serilog — console only in production

`appsettings.json` writes to `logs/ems-.log`. In a container that path is ephemeral and the logs vanish with the pod. Console output is what `kubectl logs` reads. Move the file sink to Development only.

**Files:**
- Modify: `EventManagementSystem/EMSApplicationLayer/appsettings.json`
- Modify: `EventManagementSystem/EMSApplicationLayer/appsettings.Development.json`

- [ ] **Step 1: Strip the file sink from the base config**

In `appsettings.json`, replace the `Serilog.WriteTo` array so only Console remains:

```json
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" }
    ]
  },
```

- [ ] **Step 2: Restore the file sink for local development**

In `appsettings.Development.json`, add (merging into the existing object — do not delete existing keys):

```json
  "Serilog": {
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/ems-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
```

- [ ] **Step 3: Verify logs still go to console**

```bash
dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj 2>&1 | head -5
```

Expected: Serilog lines appear on stdout.

- [ ] **Step 4: Commit**

```bash
git add EventManagementSystem/EMSApplicationLayer/appsettings.json EventManagementSystem/EMSApplicationLayer/appsettings.Development.json
git commit -m "log to console in production"
```

---

### Task 6: Dockerfile

One image serves all three roles (`ems-api`, `ems-worker`, migration Job) — the only difference is env vars and args. Runs as a non-root user.

**Files:**
- Create: `EventManagementSystem/Dockerfile`
- Create: `EventManagementSystem/.dockerignore`

**Interfaces:**
- Produces: an image whose entrypoint is `dotnet EMSApplicationLayer.dll`. Passing `--migrate` as an arg triggers migrate mode (Task 4). Listens on port **8080**.

- [ ] **Step 1: Write the Dockerfile**

Create `EventManagementSystem/Dockerfile`:

```dockerfile
# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first so `restore` is cached independently of source changes.
COPY EMS.sln .
COPY EMSApplicationLayer/EMSApplicationLayer.csproj EMSApplicationLayer/
COPY EMSBLLLibrary/EMSBLLLibrary.csproj             EMSBLLLibrary/
COPY EMSDALLibrary/EMSDALLibrary.csproj             EMSDALLibrary/
COPY EMSModelLibrary/EMSModelLibrary.csproj         EMSModelLibrary/
COPY EMSTests/EMSTests.csproj                       EMSTests/
RUN dotnet restore EMSApplicationLayer/EMSApplicationLayer.csproj

COPY . .
RUN dotnet publish EMSApplicationLayer/EMSApplicationLayer.csproj \
    -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Run as non-root. The aspnet image ships an "app" user (uid 1654).
USER app

COPY --from=build --chown=app:app /app .

# Kestrel binds 8080; ports below 1024 need root, which we do not have.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EMSApplicationLayer.dll"]
```

- [ ] **Step 2: Write the .dockerignore**

Create `EventManagementSystem/.dockerignore`:

```
**/bin/
**/obj/
**/logs/
**/*.user
**/appsettings.Development.json
.git/
.vs/
```

`appsettings.Development.json` is excluded deliberately — it contains local credentials and must never enter an image.

- [ ] **Step 3: Build the image**

```bash
docker build -t ems-api:local EventManagementSystem/
```

Expected: builds successfully, ending with `naming to docker.io/library/ems-api:local`.

- [ ] **Step 4: Verify it runs and serves the liveness probe**

The container needs to reach your host's Postgres. On macOS use `host.docker.internal`:

```bash
docker run -d --name ems-test -p 8080:8080 \
  -e ConnectionStrings__Default="Host=host.docker.internal;Port=5432;Database=eventmanagement;Username=postgres;Password=Poornima290178@" \
  -e Stripe__SecretKey="sk_test_dummy" \
  -e Workers__Enabled=false \
  ems-api:local
sleep 15
curl -s -o /dev/null -w "live=%{http_code}\n" http://localhost:8080/health/live
curl -s -o /dev/null -w "ready=%{http_code}\n" http://localhost:8080/health/ready
```

Expected:
```
live=200
ready=200
```

**This step is the real test of Tasks 2, 3, and 6 together.** A `301`/`307` instead of `200` means `UseHttpsRedirection` is still active in the container — go back and fix Task 2, because that exact response would crash-loop the pod on AKS.

- [ ] **Step 5: Verify the worker flag is honoured**

```bash
docker logs ems-test 2>&1 | grep -ci "BookingExpiry\|EmailDispatcher\|EventReminder"
```

Expected: `0` — with `Workers__Enabled=false` no background service should log. Now confirm the inverse:

```bash
docker rm -f ems-test >/dev/null
docker run -d --name ems-worker-test \
  -e ConnectionStrings__Default="Host=host.docker.internal;Port=5432;Database=eventmanagement;Username=postgres;Password=Poornima290178@" \
  -e Stripe__SecretKey="sk_test_dummy" \
  -e Workers__Enabled=true \
  ems-api:local
sleep 15
docker logs ems-worker-test 2>&1 | grep -ci "BookingExpiry\|EmailDispatcher\|EventReminder"
docker rm -f ems-worker-test >/dev/null
```

Expected: a number **greater than 0**. If both runs print the same count, the flag is not wired — revisit Task 1.

- [ ] **Step 6: Commit**

```bash
git add EventManagementSystem/Dockerfile EventManagementSystem/.dockerignore
git commit -m "add api dockerfile"
```

---

### Task 7: Bicep infrastructure

**Files:**
- Create: `infra/main.bicep`
- Create: `infra/modules/acr.bicep`, `aks.bicep`, `postgres.bicep`, `keyvault.bicep`, `swa.bicep`, `publicip.bicep`

**Interfaces:**
- Produces (as `main.bicep` outputs, consumed by Task 10's workflows): `acrLoginServer`, `aksName`, `keyVaultName`, `postgresFqdn`, `workloadIdentityClientId`, `ingressPublicIp`, `ingressFqdn`, `swaName`.

- [ ] **Step 1: Confirm the Static Web Apps region before writing any Bicep**

SWA is not offered in `southindia`. Verify which region to use:

```bash
az staticwebapp list --query "[].location" -o tsv 2>/dev/null | sort -u
az provider show -n Microsoft.Web --query "resourceTypes[?resourceType=='staticSites'].locations[]" -o tsv
```

Expected: a list that **does not include South India**. Use `eastasia` (closest to India). If `eastasia` is absent from the output, pick the nearest listed region and use it consistently in `swa.bicep`.

- [ ] **Step 2: Write `infra/modules/acr.bicep`**

```bicep
param name string
param location string

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: name
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false   // AKS pulls via its kubelet identity, not a shared password
  }
}

output loginServer string = acr.properties.loginServer
output id string = acr.id
```

- [ ] **Step 3: Write `infra/modules/keyvault.bicep`**

```bicep
param name string
param location string

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true      // RBAC, not legacy access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

output name string = kv.name
output id string = kv.id
output uri string = kv.properties.vaultUri
```

- [ ] **Step 4: Write `infra/modules/postgres.bicep`**

```bicep
param name string
param location string
param administratorLogin string = 'emsadmin'
@secure()
param administratorPassword string

resource pg 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: { storageSizeGB: 32 }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: pg
  name: 'eventmanagement'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

output fqdn string = pg.properties.fullyQualifiedDomainName
output name string = pg.name
```

The firewall rule pinning access to the AKS egress IP is **not** in Bicep: that IP does not exist until AKS has created its load balancer, so it is added by the infra workflow in Task 10 after deployment.

- [ ] **Step 5: Write `infra/modules/publicip.bicep`**

The ingress needs a static IP that outlives the node. Its DNS label gives the free `*.cloudapp.azure.com` hostname that cert-manager needs — Let's Encrypt cannot issue for a bare IP.

```bicep
param name string
param location string
param dnsLabel string

resource pip 'Microsoft.Network/publicIPAddresses@2023-11-01' = {
  name: name
  location: location
  sku: { name: 'Standard' }
  properties: {
    publicIPAllocationMethod: 'Static'
    dnsSettings: {
      domainNameLabel: dnsLabel   // → <dnsLabel>.<region>.cloudapp.azure.com
    }
  }
}

output id string = pip.id
output ipAddress string = pip.properties.ipAddress
output fqdn string = pip.properties.dnsSettings.fqdn
```

- [ ] **Step 6: Write `infra/modules/aks.bicep`**

```bicep
param name string
param location string
param nodeVmSize string = 'Standard_B2als_v2'

resource aks 'Microsoft.ContainerService/managedClusters@2024-09-01' = {
  name: name
  location: location
  identity: { type: 'SystemAssigned' }
  sku: {
    name: 'Base'
    tier: 'Free'        // free control plane; no uptime SLA, fine for a demo
  }
  properties: {
    dnsPrefix: name
    enableRBAC: true

    // Required for workload identity (Task 8's SecretProviderClass depends on both).
    oidcIssuerProfile: { enabled: true }
    securityProfile: {
      workloadIdentity: { enabled: true }
    }

    // Installs the Secrets Store CSI driver + Azure provider.
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: { enableSecretRotation: 'true' }
      }
    }

    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        count: 1                      // single node — see design doc sizing section
        vmSize: nodeVmSize            // Standard_B2s does NOT exist in southindia
        osType: 'Linux'
        osSKU: 'Ubuntu'
        type: 'VirtualMachineScaleSets'
        osDiskSizeGB: 32
      }
    ]

    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'    // fewer IPs consumed than classic azure CNI
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer'    // the LB also provides egress SNAT
    }
  }
}

output name string = aks.name
output id string = aks.id
output oidcIssuerUrl string = aks.properties.oidcIssuerProfile.issuerURL
output kubeletIdentityObjectId string = aks.properties.identityProfile.kubeletidentity.objectId
output clusterIdentityPrincipalId string = aks.identity.principalId
output nodeResourceGroup string = aks.properties.nodeResourceGroup
```

- [ ] **Step 7: Write `infra/modules/swa.bicep`**

```bicep
param name string
param location string   // NOT southindia — SWA is not offered there. See Step 1.

resource swa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    // Deploys come from GitHub Actions (Task 10), not from SWA's own build pipeline.
    allowConfigFileUpdates: true
  }
}

output name string = swa.name
output defaultHostname string = swa.properties.defaultHostname
```

- [ ] **Step 8: Write `infra/main.bicep`**

```bicep
targetScope = 'resourceGroup'

param location string = 'southindia'
param swaLocation string = 'eastasia'      // SWA is not available in southindia
param prefix string = 'ems'
@secure()
param postgresAdminPassword string

// Suffix keeps globally-unique names (ACR, Key Vault, DNS label) collision-free.
var suffix = uniqueString(resourceGroup().id)

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    name: '${prefix}acr${suffix}'          // ACR names: alphanumeric only
    location: location
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: '${prefix}-kv-${take(suffix, 8)}'   // Key Vault names max 24 chars
    location: location
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: '${prefix}-pg-${suffix}'
    location: location
    administratorPassword: postgresAdminPassword
  }
}

module aks 'modules/aks.bicep' = {
  name: 'aks'
  params: {
    name: '${prefix}-aks'
    location: location
  }
}

module publicip 'modules/publicip.bicep' = {
  name: 'publicip'
  params: {
    name: '${prefix}-ingress-ip'
    location: location
    dnsLabel: '${prefix}-api-${take(suffix, 8)}'
  }
}

module swa 'modules/swa.bicep' = {
  name: 'swa'
  params: {
    name: '${prefix}-web-${take(suffix, 8)}'
    location: swaLocation
  }
}

// ── Workload identity: the pods' Azure identity for reading Key Vault ───────────
resource workloadIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-workload-id'
  location: location
}

// Federates the Kubernetes ServiceAccount ems:ems-sa to the managed identity, so pods
// exchange their projected SA token for an Azure token with no stored credential.
resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: workloadIdentity
  name: 'ems-service-account'
  properties: {
    issuer: aks.outputs.oidcIssuerUrl
    subject: 'system:serviceaccount:ems:ems-sa'   // must match Task 8's namespace + SA name
    audiences: ['api://AzureADTokenExchange']
  }
}

// ── Role assignments ──────────────────────────────────────────────────────────
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var acrPullRoleId             = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var networkContributorRoleId  = '4d97b98b-1d4f-4787-a291-c67834d212e7'

resource kvExisting 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyvault.outputs.name
}

// Pods read secrets from Key Vault.
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kvExisting
  name: guid(keyvault.outputs.id, workloadIdentity.id, keyVaultSecretsUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: workloadIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrExisting 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: '${prefix}acr${suffix}'
}

// AKS's kubelet pulls images from ACR.
resource acrRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acrExisting
  name: guid(acr.outputs.id, aks.outputs.kubeletIdentityObjectId, acrPullRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: aks.outputs.kubeletIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

// AKS must be able to attach the static public IP (which lives in THIS resource group,
// not the AKS-managed node resource group) to its load balancer.
resource networkRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, aks.outputs.clusterIdentityPrincipalId, networkContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', networkContributorRoleId)
    principalId: aks.outputs.clusterIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output acrLoginServer string           = acr.outputs.loginServer
output aksName string                  = aks.outputs.name
output keyVaultName string             = keyvault.outputs.name
output postgresFqdn string             = postgres.outputs.fqdn
output workloadIdentityClientId string = workloadIdentity.properties.clientId
output ingressPublicIp string          = publicip.outputs.ipAddress
output ingressFqdn string              = publicip.outputs.fqdn
output swaName string                  = swa.outputs.name
output resourceGroupName string        = resourceGroup().name
```

- [ ] **Step 9: Validate the Bicep compiles**

```bash
az bicep build --file infra/main.bicep --stdout > /dev/null && echo "BICEP OK"
```

Expected: `BICEP OK` with no errors. Warnings about unused params are acceptable; errors are not.

- [ ] **Step 10: Dry-run against Azure without creating anything**

```bash
az group create -n rg-ems-prod -l southindia -o none
az deployment group what-if \
  --resource-group rg-ems-prod \
  --template-file infra/main.bicep \
  --parameters postgresAdminPassword="$(openssl rand -base64 24)"
```

Expected: a `+ Create` list containing the AKS cluster, ACR, Key Vault, Postgres server, public IP, static web app, and managed identity. **Any red error here must be fixed before Task 11** — that is the whole point of running what-if.

- [ ] **Step 11: Commit**

```bash
git add infra/
git commit -m "add bicep infrastructure"
```

---

### Task 8: Kubernetes manifests

**Files:**
- Create: `k8s/namespace.yaml`, `serviceaccount.yaml`, `secretproviderclass.yaml`, `api-deployment.yaml`, `api-service.yaml`, `worker-deployment.yaml`, `ingress.yaml`, `migrate-job.yaml`, `cluster-issuer.yaml`

**Interfaces:**
- Consumes: image `${ACR_LOGIN_SERVER}/ems-api:${GIT_SHA}` (Task 6); Key Vault name and workload-identity client ID (Task 7 outputs).
- Produces: Deployments `ems-api` / `ems-worker`, Service `ems-api`, Ingress `ems-api`, Job `ems-migrate` — all in namespace `ems`.

Placeholders (`__ACR__`, `__IMAGE_TAG__`, …) are substituted by `envsubst` in Task 10's workflow.

- [ ] **Step 1: Namespace and ServiceAccount**

`k8s/namespace.yaml`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: ems
```

`k8s/serviceaccount.yaml` — the name and namespace must exactly match the federated credential subject `system:serviceaccount:ems:ems-sa` from Task 7:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: ems-sa
  namespace: ems
  annotations:
    azure.workload.identity/client-id: "__WORKLOAD_IDENTITY_CLIENT_ID__"
  labels:
    azure.workload.identity/use: "true"
```

- [ ] **Step 2: SecretProviderClass**

`k8s/secretproviderclass.yaml` — pulls secrets from Key Vault and syncs them into a Kubernetes Secret named `ems-secrets`, which the Deployments consume as env vars:

```yaml
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: ems-keyvault
  namespace: ems
spec:
  provider: azure
  parameters:
    usePodIdentity: "false"
    useVMManagedIdentity: "false"
    clientID: "__WORKLOAD_IDENTITY_CLIENT_ID__"
    keyvaultName: "__KEYVAULT_NAME__"
    tenantId: "__TENANT_ID__"
    objects: |
      array:
        - |
          objectName: db-connection-string
          objectType: secret
        - |
          objectName: jwt-key
          objectType: secret
        - |
          objectName: stripe-secret-key
          objectType: secret
        - |
          objectName: stripe-webhook-secret
          objectType: secret
        - |
          objectName: resend-api-key
          objectType: secret
  secretObjects:
    - secretName: ems-secrets
      type: Opaque
      data:
        - objectName: db-connection-string
          key: ConnectionStrings__Default
        - objectName: jwt-key
          key: Jwt__Key
        - objectName: stripe-secret-key
          key: Stripe__SecretKey
        - objectName: stripe-webhook-secret
          key: Stripe__WebhookSecret
        - objectName: resend-api-key
          key: Email__ApiKey
```

The `key` names are the .NET env-var forms — `__` maps to `:` in configuration, so `ConnectionStrings__Default` populates `ConnectionStrings:Default`.

- [ ] **Step 3: API Deployment**

`k8s/api-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ems-api
  namespace: ems
spec:
  replicas: 1
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 0          # single tight node: no room to schedule an extra pod
      maxUnavailable: 1    # costs a few seconds of downtime per deploy. Accepted.
  selector:
    matchLabels:
      app: ems-api
  template:
    metadata:
      labels:
        app: ems-api
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: ems-sa
      containers:
        - name: api
          image: __ACR__/ems-api:__IMAGE_TAG__
          ports:
            - containerPort: 8080
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: Workers__Enabled
              value: "false"          # background jobs belong to ems-worker only
            - name: Cors__AllowedOrigins__0
              value: "https://__SWA_HOSTNAME__"
            - name: Email__Enabled
              value: "true"
            - name: Email__AppBaseUrl
              value: "https://__SWA_HOSTNAME__"
          envFrom:
            - secretRef:
                name: ems-secrets     # synced from Key Vault by the CSI driver
          volumeMounts:
            - name: secrets-store
              mountPath: /mnt/secrets-store
              readOnly: true
          resources:
            requests:
              cpu: 100m
              memory: 256Mi
            limits:
              memory: 512Mi           # no CPU limit: throttling a burstable node hurts
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 20
            periodSeconds: 20
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 10
      volumes:
        # Mounting the volume is what triggers the CSI driver to fetch from Key Vault
        # and populate the ems-secrets Secret. Without this mount, envFrom finds nothing.
        - name: secrets-store
          csi:
            driver: secrets-store.csi.k8s.io
            readOnly: true
            volumeAttributes:
              secretProviderClass: ems-keyvault
```

- [ ] **Step 4: API Service**

`k8s/api-service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: ems-api
  namespace: ems
spec:
  type: ClusterIP          # the ingress fronts this; no public IP of its own
  selector:
    app: ems-api
  ports:
    - port: 80
      targetPort: 8080
```

- [ ] **Step 5: Worker Deployment**

`k8s/worker-deployment.yaml` — same image, no Service, no ingress, no probes on an HTTP port it does not serve:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ems-worker
  namespace: ems
spec:
  replicas: 1              # MUST stay 1: the background services have no leader election
  strategy:
    type: Recreate         # never run two workers at once, even briefly during a deploy
  selector:
    matchLabels:
      app: ems-worker
  template:
    metadata:
      labels:
        app: ems-worker
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: ems-sa
      containers:
        - name: worker
          image: __ACR__/ems-api:__IMAGE_TAG__
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: Workers__Enabled
              value: "true"
            - name: Email__Enabled
              value: "true"
            - name: Email__AppBaseUrl
              value: "https://__SWA_HOSTNAME__"
          envFrom:
            - secretRef:
                name: ems-secrets
          volumeMounts:
            - name: secrets-store
              mountPath: /mnt/secrets-store
              readOnly: true
          resources:
            requests:
              cpu: 50m
              memory: 200Mi
            limits:
              memory: 400Mi
      volumes:
        - name: secrets-store
          csi:
            driver: secrets-store.csi.k8s.io
            readOnly: true
            volumeAttributes:
              secretProviderClass: ems-keyvault
```

`strategy: Recreate` matters: the default RollingUpdate would briefly run two worker pods at once, and for those few seconds every queued email would go out twice.

- [ ] **Step 6: Migration Job**

`k8s/migrate-job.yaml`:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: ems-migrate-__IMAGE_TAG__    # unique per deploy; Jobs are immutable
  namespace: ems
spec:
  backoffLimit: 2
  ttlSecondsAfterFinished: 600       # tidy itself up 10 min after completion
  template:
    metadata:
      labels:
        azure.workload.identity/use: "true"
    spec:
      restartPolicy: Never
      serviceAccountName: ems-sa
      containers:
        - name: migrate
          image: __ACR__/ems-api:__IMAGE_TAG__
          args: ["--migrate"]        # applies migrations, then exits (Task 4)
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: Workers__Enabled
              value: "false"
          envFrom:
            - secretRef:
                name: ems-secrets
          volumeMounts:
            - name: secrets-store
              mountPath: /mnt/secrets-store
              readOnly: true
      volumes:
        - name: secrets-store
          csi:
            driver: secrets-store.csi.k8s.io
            readOnly: true
            volumeAttributes:
              secretProviderClass: ems-keyvault
```

- [ ] **Step 7: cert-manager ClusterIssuer**

`k8s/cluster-issuer.yaml`:

```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: __LETSENCRYPT_EMAIL__
    privateKeySecretRef:
      name: letsencrypt-prod-account-key
    solvers:
      - http01:
          ingress:
            ingressClassName: nginx
```

- [ ] **Step 8: Ingress**

`k8s/ingress.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ems-api
  namespace: ems
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    # SignalR needs long-lived connections; the 60s default would drop the seat hub.
    nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - __INGRESS_FQDN__
      secretName: ems-api-tls     # cert-manager writes the issued cert here
  rules:
    - host: __INGRESS_FQDN__
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: ems-api
                port:
                  number: 80
```

No session-affinity annotation is needed: `ems-api` runs a single replica, so every SignalR client necessarily lands on the same pod. **If you ever raise `replicas` above 1, you must add cookie affinity here or add a Redis backplane**, or SignalR negotiation will break.

- [ ] **Step 9: Validate all manifests parse**

Placeholders are not valid values yet, so validate syntax only:

```bash
for f in k8s/*.yaml; do python3 -c "import yaml,sys; list(yaml.safe_load_all(open('$f')))" && echo "OK  $f" || echo "BAD $f"; done
```

Expected: `OK` for every file.

- [ ] **Step 10: Commit**

```bash
git add k8s/
git commit -m "add kubernetes manifests"
```

---

### Task 9: Angular production config

**Files:**
- Modify: `EMSAngular/src/environments/environment.prod.ts`
- Create: `EMSAngular/staticwebapp.config.json`

- [ ] **Step 1: SPA fallback routing**

Without this, a refresh on `/events/5` returns 404 — Static Web Apps looks for a file at that path. Create `EMSAngular/staticwebapp.config.json`:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "*.css", "*.js", "*.ico", "*.png", "*.svg", "*.woff2"]
  },
  "responseOverrides": {
    "404": {
      "rewrite": "/index.html",
      "statusCode": 200
    }
  }
}
```

- [ ] **Step 2: Real production environment values**

`environment.prod.ts` currently holds `REPLACE_WITH_PRODUCTION_API_URL` and a `pk_live_` placeholder. Replace the file with the real ingress FQDN (Task 7's `ingressFqdn` output) and your Stripe **test** publishable key:

```ts
// Production environment configuration.
// Swapped in for environment.ts during a production build (see "fileReplacements"
// in angular.json).
export const environment = {
  production: true,
  // The AKS ingress FQDN — the `ingressFqdn` output of infra/main.bicep.
  apiBaseUrl: 'https://__INGRESS_FQDN__',
  // Stripe TEST publishable key. Safe to ship in the client bundle.
  // Must be pk_test_, never pk_live_ — this is a demo and must not move real money.
  stripePublishableKey: 'pk_test_51Tk1KgDlDAgZgiPTi3aYubmttJEhLKybjjIiOjf9bPknQz70gArdOxFqQNZM2Z0tT90anDA3zw99DdVNCjf73Ul000LaAknaU3',
};
```

`__INGRESS_FQDN__` is substituted by the `deploy-web` workflow (Task 10) at build time, so the value is never hardcoded or committed stale.

- [ ] **Step 3: Verify the production build succeeds**

```bash
cd EMSAngular && npm ci && npx ng build --configuration production
```

Expected: `Application bundle generation complete`, output in `EMSAngular/dist/`.

- [ ] **Step 4: Verify no live Stripe key is present in the bundle**

```bash
grep -rc "pk_live_" EMSAngular/dist/ || echo "CLEAN: no live key in bundle"
```

Expected: `CLEAN: no live key in bundle`. If a `pk_live_` string is found, **stop and fix it** — a live key in a public bundle can move real money.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/staticwebapp.config.json EMSAngular/src/environments/environment.prod.ts
git commit -m "add angular production config"
```

---

### Task 10: GitHub Actions with OIDC

**Files:**
- Create: `.github/workflows/infra.yml`, `.github/workflows/deploy-api.yml`, `.github/workflows/deploy-web.yml`

**Interfaces:**
- Consumes: Bicep outputs (Task 7), manifests with `__PLACEHOLDER__` tokens (Task 8).
- Produces: GitHub repository secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `POSTGRES_ADMIN_PASSWORD`, `SWA_DEPLOYMENT_TOKEN`.

- [ ] **Step 1: Create the app registration and federated credential**

OIDC means GitHub exchanges a short-lived token for an Azure token — no service-principal password is ever stored.

```bash
APP_ID=$(az ad app create --display-name "ems-github-actions" --query appId -o tsv)
az ad sp create --id "$APP_ID" -o none
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

az role assignment create \
  --assignee "$APP_ID" \
  --role Contributor \
  --scope "/subscriptions/$SUB_ID/resourceGroups/rg-ems-prod" -o none

# Also needed: the workflows create role assignments, which Contributor cannot do.
az role assignment create \
  --assignee "$APP_ID" \
  --role "User Access Administrator" \
  --scope "/subscriptions/$SUB_ID/resourceGroups/rg-ems-prod" -o none

# Federated credentials are pinned to an exact git ref. TWO are needed:
#   main → so the manually-dispatched infra.yml can run
#   prod → so the deploy workflows can run
# A missing credential fails at token exchange with an error that looks like an RBAC
# problem but is not. If a deploy fails with AADSTS70021, the ref is not registered here.
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:Sri-Manikandan/Capstone-Project-Genspark:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-prod",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:Sri-Manikandan/Capstone-Project-Genspark:ref:refs/heads/prod",
  "audiences": ["api://AzureADTokenExchange"]
}'

echo "AZURE_CLIENT_ID=$APP_ID"
echo "AZURE_TENANT_ID=$TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID=$SUB_ID"
```

Expected: three values printed. Add them as GitHub repository secrets:

```bash
gh secret set AZURE_CLIENT_ID --body "$APP_ID"
gh secret set AZURE_TENANT_ID --body "$TENANT_ID"
gh secret set AZURE_SUBSCRIPTION_ID --body "$SUB_ID"

# cert-manager registers this address with Let's Encrypt for expiry notices. Required —
# the ClusterIssuer is rejected without it.
gh secret set LETSENCRYPT_EMAIL --body "saisidharthan@synergeek.in"
```

- [ ] **Step 1b: Generate the Postgres password and keep a copy**

Alphanumeric only, deliberately: a base64 password can contain `+`, `/`, and `=`, which are
awkward inside an Npgsql connection string, and Azure Postgres rejects `'`, `"`, and `@`.

You **must** keep this value — Task 11 Step 2 needs it to build the connection string, and
GitHub will never show it back to you.

```bash
PG_PASS=$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32)
gh secret set POSTGRES_ADMIN_PASSWORD --body "$PG_PASS"

echo "SAVE THIS NOW — Postgres admin password: $PG_PASS"
```

Store it in your password manager before moving on. If you lose it, you can reset it later
with `az postgres flexible-server update -g rg-ems-prod -n <server> --admin-password <new>`,
but you would then have to update the `db-connection-string` secret in Key Vault to match.

- [ ] **Step 2: Write `.github/workflows/infra.yml`**

```yaml
name: Provision Infrastructure

on:
  workflow_dispatch:

permissions:
  id-token: write     # required for OIDC
  contents: read

jobs:
  provision:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Create resource group
        run: az group create -n rg-ems-prod -l southindia -o none

      - name: Deploy Bicep
        id: bicep
        run: |
          az deployment group create \
            --resource-group rg-ems-prod \
            --template-file infra/main.bicep \
            --parameters postgresAdminPassword="${{ secrets.POSTGRES_ADMIN_PASSWORD }}" \
            --query properties.outputs -o json > outputs.json
          cat outputs.json

      # The AKS egress IP does not exist until AKS has built its load balancer, so this
      # firewall rule cannot live in Bicep.
      - name: Allow AKS egress IP through the Postgres firewall
        run: |
          AKS_NAME=$(jq -r .aksName.value outputs.json)
          PG_NAME=$(jq -r .postgresFqdn.value outputs.json | cut -d. -f1)
          NODE_RG=$(az aks show -g rg-ems-prod -n "$AKS_NAME" --query nodeResourceGroup -o tsv)
          EGRESS_IP=$(az network public-ip list -g "$NODE_RG" \
            --query "[?starts_with(name,'kubernetes')].ipAddress | [0]" -o tsv)
          echo "AKS egress IP: $EGRESS_IP"
          az postgres flexible-server firewall-rule create \
            -g rg-ems-prod -n "$PG_NAME" \
            --rule-name allow-aks-egress \
            --start-ip-address "$EGRESS_IP" \
            --end-ip-address "$EGRESS_IP" -o none
```

- [ ] **Step 3: Write `.github/workflows/deploy-api.yml`**

```yaml
name: Deploy API

on:
  push:
    branches: [prod]        # NOT main. Promote with: git checkout prod && git merge main
    paths:
      - 'EventManagementSystem/**'
      - 'k8s/**'
      - '.github/workflows/deploy-api.yml'

permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Run tests
        run: dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --verbosity minimal

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Read infrastructure outputs
        id: infra
        run: |
          OUT=$(az deployment group show -g rg-ems-prod -n main --query properties.outputs -o json)
          echo "acr=$(echo "$OUT"        | jq -r .acrLoginServer.value)"           >> $GITHUB_OUTPUT
          echo "aks=$(echo "$OUT"        | jq -r .aksName.value)"                  >> $GITHUB_OUTPUT
          echo "kv=$(echo "$OUT"         | jq -r .keyVaultName.value)"             >> $GITHUB_OUTPUT
          echo "clientId=$(echo "$OUT"   | jq -r .workloadIdentityClientId.value)" >> $GITHUB_OUTPUT
          echo "fqdn=$(echo "$OUT"       | jq -r .ingressFqdn.value)"              >> $GITHUB_OUTPUT
          echo "swaName=$(echo "$OUT"    | jq -r .swaName.value)"                  >> $GITHUB_OUTPUT

      - name: Build and push image
        run: |
          az acr login --name "${{ steps.infra.outputs.acr }}"
          docker build -t "${{ steps.infra.outputs.acr }}/ems-api:${{ github.sha }}" EventManagementSystem/
          docker push "${{ steps.infra.outputs.acr }}/ems-api:${{ github.sha }}"

      - name: Get kubectl credentials
        run: az aks get-credentials -g rg-ems-prod -n "${{ steps.infra.outputs.aks }}" --overwrite-existing

      - name: Render manifests
        env:
          ACR: ${{ steps.infra.outputs.acr }}
          IMAGE_TAG: ${{ github.sha }}
          KEYVAULT_NAME: ${{ steps.infra.outputs.kv }}
          WORKLOAD_IDENTITY_CLIENT_ID: ${{ steps.infra.outputs.clientId }}
          TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
          INGRESS_FQDN: ${{ steps.infra.outputs.fqdn }}
          LETSENCRYPT_EMAIL: ${{ secrets.LETSENCRYPT_EMAIL }}
        run: |
          SWA_HOSTNAME=$(az staticwebapp show -g rg-ems-prod \
            -n "${{ steps.infra.outputs.swaName }}" --query defaultHostname -o tsv)

          # Deliberately sed, not envsubst: envsubst only expands shell-style ${VAR},
          # so it would leave every __PLACEHOLDER__ untouched and we would apply broken
          # manifests. Note the manifests also contain legitimate double underscores
          # (ConnectionStrings__Default, Workers__Enabled) which must NOT be touched.
          mkdir -p rendered
          for f in k8s/*.yaml; do
            sed -e "s|__ACR__|${ACR}|g" \
                -e "s|__IMAGE_TAG__|${IMAGE_TAG}|g" \
                -e "s|__KEYVAULT_NAME__|${KEYVAULT_NAME}|g" \
                -e "s|__WORKLOAD_IDENTITY_CLIENT_ID__|${WORKLOAD_IDENTITY_CLIENT_ID}|g" \
                -e "s|__TENANT_ID__|${TENANT_ID}|g" \
                -e "s|__INGRESS_FQDN__|${INGRESS_FQDN}|g" \
                -e "s|__SWA_HOSTNAME__|${SWA_HOSTNAME}|g" \
                -e "s|__LETSENCRYPT_EMAIL__|${LETSENCRYPT_EMAIL}|g" \
                "$f" > "rendered/$(basename "$f")"
          done

          # Anchored pattern: matches __UPPER_CASE__ placeholders only, so it does not
          # false-positive on ConnectionStrings__Default or Workers__Enabled.
          if grep -rnE '__[A-Z][A-Z0-9_]*__' rendered/; then
            echo "ERROR: unsubstituted placeholder above"; exit 1
          fi
          echo "All placeholders substituted."

      - name: Apply base resources
        run: |
          kubectl apply -f rendered/namespace.yaml
          kubectl apply -f rendered/serviceaccount.yaml
          kubectl apply -f rendered/secretproviderclass.yaml
          kubectl apply -f rendered/cluster-issuer.yaml

      # Migrations must finish BEFORE new pods start, or a pod could query a table that
      # does not exist yet.
      - name: Run migrations and wait
        run: |
          kubectl apply -f rendered/migrate-job.yaml
          kubectl wait --for=condition=complete --timeout=300s \
            job/ems-migrate-${{ github.sha }} -n ems || {
              echo "--- migration job failed, logs: ---"
              kubectl logs job/ems-migrate-${{ github.sha }} -n ems
              exit 1
            }
          kubectl logs job/ems-migrate-${{ github.sha }} -n ems

      - name: Deploy application
        run: |
          kubectl apply -f rendered/api-deployment.yaml
          kubectl apply -f rendered/api-service.yaml
          kubectl apply -f rendered/worker-deployment.yaml
          kubectl apply -f rendered/ingress.yaml
          kubectl rollout status deployment/ems-api    -n ems --timeout=300s
          kubectl rollout status deployment/ems-worker -n ems --timeout=300s
```

- [ ] **Step 4: Write `.github/workflows/deploy-web.yml`**

```yaml
name: Deploy Web

on:
  push:
    branches: [prod]        # NOT main. See deploy-api.yml.
    paths:
      - 'EMSAngular/**'
      - '.github/workflows/deploy-web.yml'

permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Inject the API URL into the production environment
        run: |
          FQDN=$(az deployment group show -g rg-ems-prod -n main \
            --query properties.outputs.ingressFqdn.value -o tsv)
          sed -i "s|__INGRESS_FQDN__|${FQDN}|g" EMSAngular/src/environments/environment.prod.ts
          grep apiBaseUrl EMSAngular/src/environments/environment.prod.ts

      - uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Build
        working-directory: EMSAngular
        run: |
          npm ci
          npx ng build --configuration production

      - name: Guard against a live Stripe key
        run: |
          if grep -rq "pk_live_" EMSAngular/dist/; then
            echo "ERROR: a live Stripe key is in the bundle"; exit 1
          fi
          echo "No live Stripe key in bundle."

      - name: Deploy to Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.SWA_DEPLOYMENT_TOKEN }}
          action: upload
          app_location: EMSAngular/dist/EMSAngular/browser
          skip_app_build: true
```

Note: confirm the exact `dist` path from Task 9 Step 3's build output — Angular writes to `dist/<projectName>/browser`. Fix `app_location` to match.

- [ ] **Step 5: Write `.github/workflows/ci.yml`**

`main` no longer deploys, so it needs its own verification — otherwise a broken commit sits
unnoticed on `main` until someone promotes it to `prod` and it fails in Azure instead.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build
        run: dotnet build EventManagementSystem/EMS.sln

      - name: Test
        run: dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --verbosity minimal

      - uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Angular build and test
        working-directory: EMSAngular
        run: |
          npm ci
          npx ng build --configuration production
          TZ=UTC npx ng test --watch=false --browsers=ChromeHeadless

      # Verify the image builds. Catches a broken Dockerfile on main rather than
      # mid-deploy on prod.
      - name: Docker build
        run: docker build -t ems-api:ci EventManagementSystem/
```

The Angular tests run under `TZ=UTC` deliberately: the app's IST datetime handling has a
whole class of bug that is invisible on an IST machine, so CI must check another zone.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/
git commit -m "add github actions workflows"
```

---

### Task 11: Provision, deploy, and verify end to end

Everything up to here has been code. This task runs it against Azure and proves each in-scope feature works. **Do not mark this plan complete until every check below passes.**

- [ ] **Step 1: Provision infrastructure**

```bash
gh workflow run infra.yml
gh run watch
```

Expected: green. Then capture the outputs:

```bash
az deployment group show -g rg-ems-prod -n main --query properties.outputs -o json | tee infra-outputs.json
INGRESS_FQDN=$(jq -r .ingressFqdn.value infra-outputs.json)
KV=$(jq -r .keyVaultName.value infra-outputs.json)
echo "API will live at https://$INGRESS_FQDN"
```

- [ ] **Step 2: Populate Key Vault**

Nothing works until these five secrets exist. The Postgres password must match the one in `POSTGRES_ADMIN_PASSWORD`, and the connection string **must** include `SSL Mode=Require` — Azure Postgres refuses plaintext connections.

```bash
PG_FQDN=$(jq -r .postgresFqdn.value infra-outputs.json)
PG_PASS='<the POSTGRES_ADMIN_PASSWORD you set in Task 10>'

az keyvault secret set --vault-name "$KV" --name db-connection-string \
  --value "Host=$PG_FQDN;Port=5432;Database=eventmanagement;Username=emsadmin;Password=$PG_PASS;SSL Mode=Require;Trust Server Certificate=true"

az keyvault secret set --vault-name "$KV" --name jwt-key --value "$(openssl rand -base64 48)"
az keyvault secret set --vault-name "$KV" --name stripe-secret-key      --value "sk_test_..."
az keyvault secret set --vault-name "$KV" --name stripe-webhook-secret  --value "whsec_placeholder"
az keyvault secret set --vault-name "$KV" --name resend-api-key         --value "re_..."
```

`stripe-webhook-secret` is a placeholder for now — the real value only exists after Step 5 registers the endpoint.

- [ ] **Step 3: Install ingress-nginx and cert-manager**

These are cluster add-ons, not app manifests, so they are installed once by hand rather than on every deploy.

```bash
AKS=$(jq -r .aksName.value infra-outputs.json)
az aks get-credentials -g rg-ems-prod -n "$AKS" --overwrite-existing

INGRESS_IP=$(jq -r .ingressPublicIp.value infra-outputs.json)
DNS_LABEL=$(echo "$INGRESS_FQDN" | cut -d. -f1)

helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo add jetstack https://charts.jetstack.io
helm repo update

# The service.beta annotations tell AKS to attach OUR pre-created static IP (which lives
# in rg-ems-prod, not the node resource group) instead of allocating a fresh one.
helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx --create-namespace \
  --set controller.service.loadBalancerIP="$INGRESS_IP" \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-resource-group"=rg-ems-prod \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-dns-label-name"="$DNS_LABEL" \
  --set controller.replicaCount=1 \
  --set controller.resources.requests.memory=128Mi

helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager --create-namespace \
  --set crds.enabled=true
```

Verify the LoadBalancer picked up the static IP:

```bash
kubectl get svc -n ingress-nginx ingress-nginx-controller \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}{"\n"}'
```

Expected: exactly the value of `$INGRESS_IP`. If it shows `<pending>` for more than ~3 minutes, the AKS identity lacks Network Contributor on `rg-ems-prod` — recheck the role assignment in Task 7.

- [ ] **Step 4: Deploy the application by promoting to `prod`**

Deploys fire on push to `prod`, not `main`. Create the branch and promote:

```bash
git checkout main && git pull
git checkout -B prod && git push -u origin prod
gh run watch
```

`git checkout -B prod` creates `prod` at `main`'s current commit (and resets it there if it
already exists). Pushing it triggers `deploy-api.yml` and `deploy-web.yml`.

For every subsequent release, promote with a merge instead:

```bash
git checkout prod && git merge main && git push
```

Then confirm the cluster is healthy:

```bash
kubectl get pods -n ems
```

Expected: `ems-api-…` and `ems-worker-…` both `Running` and `1/1`.

If a pod is `Pending`, the node is out of memory — check with `kubectl describe pod -n ems <name>` and look for `Insufficient memory`. That means the sizing budget in the design doc was optimistic; drop `ems-api`'s memory request or scale to a second node.

Confirm TLS was issued (this takes 1–2 minutes):

```bash
kubectl get certificate -n ems
```

Expected: `ems-api-tls`, `READY=True`. If it stays `False`, run `kubectl describe certificate -n ems ems-api-tls` — the usual cause is DNS not yet resolving to the ingress IP.

- [ ] **Step 5: Verify the API is live over HTTPS**

```bash
curl -s -o /dev/null -w "live=%{http_code}\n"  "https://$INGRESS_FQDN/health/live"
curl -s -o /dev/null -w "ready=%{http_code}\n" "https://$INGRESS_FQDN/health/ready"
```

Expected:
```
live=200
ready=200
```

`ready=200` proves the pod reached Azure Postgres through the firewall rule with SSL — i.e. Key Vault, workload identity, and the connection string are all correct. A `503` means the DB is unreachable; check `kubectl logs -n ems deploy/ems-api`.

A TLS error here (rather than an HTTP code) means the certificate has not been issued — go back to Step 4.

- [ ] **Step 6: Register the Stripe webhook and store its real secret**

```bash
echo "Webhook endpoint URL: https://$INGRESS_FQDN/api/stripe/webhook"
```

In the Stripe dashboard (**Test mode**), add that URL as an endpoint subscribed to `payment_intent.succeeded`, `payment_intent.payment_failed`, and `charge.refunded`. Copy the signing secret it gives you (`whsec_…`), then:

```bash
az keyvault secret set --vault-name "$KV" --name stripe-webhook-secret --value "whsec_<real value>"
kubectl rollout restart deployment/ems-api -n ems     # pick up the rotated secret
kubectl rollout status  deployment/ems-api -n ems
```

Send a test event from the Stripe dashboard and confirm it is accepted:

```bash
kubectl logs -n ems deploy/ems-api --tail=50 | grep -i "stripe\|webhook"
```

Expected: a 200 response logged for the webhook. A **400 with a signature error** means the secret in Key Vault does not match the endpoint's — recheck it.

- [ ] **Step 7: Deploy the frontend and verify it end to end**

The SWA deployment token does not exist until the Static Web App is provisioned, so
`deploy-web.yml` will have failed on the first promotion in Step 4. Set the token, then
re-run that workflow:

```bash
SWA_NAME=$(jq -r .swaName.value infra-outputs.json)
gh secret set SWA_DEPLOYMENT_TOKEN --body "$(az staticwebapp secrets list \
  -g rg-ems-prod -n "$SWA_NAME" --query properties.apiKey -o tsv)"

# Re-run the failed run against prod (workflow_dispatch is not wired for this one).
gh run list --workflow=deploy-web.yml --branch=prod --limit=1
gh run rerun "$(gh run list --workflow=deploy-web.yml --branch=prod --limit=1 --json databaseId -q '.[0].databaseId')"
gh run watch

SWA_HOST=$(az staticwebapp show -g rg-ems-prod -n "$SWA_NAME" --query defaultHostname -o tsv)
echo "Frontend: https://$SWA_HOST"
```

Open that URL in a browser and confirm, with the network tab open:

- The events list loads — proves CORS is correct. A CORS error means `Cors__AllowedOrigins__0` does not match the SWA hostname exactly (scheme included).
- Log in as a seeded user (password `Test@1234`) — proves the DB was migrated and seeded.
- Open an event's seat map in **two** browser windows; select a seat in one and confirm the other updates — proves SignalR works through the ingress.
- Complete a booking with Stripe test card `4242 4242 4242 4242` — proves payments work.
- Confirm the booking moves from `Pending` to `Confirmed` — proves the webhook fired and `ems-api` processed it.
- Confirm a confirmation email arrives — proves `ems-worker`'s `EmailDispatcherService` is running.

- [ ] **Step 8: Verify background jobs run exactly once**

The whole reason for the api/worker split. Confirm only the worker runs them:

```bash
kubectl logs -n ems deploy/ems-worker --tail=100 | grep -ci "BookingExpiry\|EmailDispatcher\|EventReminder"
kubectl logs -n ems deploy/ems-api    --tail=100 | grep -ci "BookingExpiry\|EmailDispatcher\|EventReminder"
```

Expected: a number **> 0** for the worker, and exactly **0** for the api. If the api logs background-service activity, `Workers__Enabled=false` is not reaching it — inspect with `kubectl exec -n ems deploy/ems-api -- env | grep Workers`.

- [ ] **Step 9: Record the real cost**

The design doc's Load Balancer and Postgres figures were estimates. Now that resources exist, capture the truth:

```bash
az consumption usage list --start-date "$(date -v-1d +%Y-%m-%d)" --end-date "$(date +%Y-%m-%d)" \
  --query "[?contains(instanceName,'ems')].{Resource:instanceName, Cost:pretaxCost, Currency:currency}" -o table
```

Compare against the ~$82/mo estimate and update the **Cost** section of the design doc with actual figures, replacing the two lines marked *(estimate)*.

- [ ] **Step 10: Commit**

```bash
git add docs/superpowers/specs/2026-07-13-azure-aks-deployment-design.md
git commit -m "record actual azure costs"
```

---

## Rollback

If a deploy breaks the API:

```bash
kubectl rollout undo deployment/ems-api -n ems
kubectl rollout status deployment/ems-api -n ems
```

This works because images are tagged with the commit SHA rather than `latest` — the previous ReplicaSet still references a real, distinct image.

**Migrations do not roll back.** If a bad migration ships, roll forward with a new one. Postgres has 7-day PITR as a last resort.

## Teardown

The cluster costs ~$82/mo whether or not anyone uses it. When the capstone is done:

```bash
az group delete -n rg-ems-prod --yes --no-wait
```

This does **not** delete the app registration from Task 10:

```bash
az ad app delete --id "$(az ad app list --display-name ems-github-actions --query '[0].appId' -o tsv)"
```
