# Azure AKS Deployment — Design

**Date:** 2026-07-13
**Status:** Approved (design), pending implementation

## Goal

Host the Event Management System on Azure: the .NET 9 API on AKS, the Angular SPA on
Static Web Apps, and PostgreSQL as a managed service. All infrastructure provisioned by
Bicep, all deploys driven by GitHub Actions.

This is a **capstone demo**, not a production system. Where a choice trades cost against
availability, cost wins — but the architecture must not embarrass itself under review, and
it must not double-send emails or lose data.

## Non-goals

- High availability. A single node means node upgrades cause downtime. Accepted.
- Autoscaling. Fixed replica counts.
- Blue/green or canary deploys. A rolling update with brief downtime is fine.
- Azure SignalR Service (~$50/mo). Not justified at one API replica.
- Container Insights. `kubectl logs` is sufficient for a demo we operate ourselves.

## Architecture

```
                          Internet
                             │
        ┌────────────────────┴─────────────────────┐
        │                                          │
  Static Web App                        Azure Standard Load Balancer
  (Angular SPA, Free tier)              static public IP + DNS label
        │                                          │
        │                                          ▼
        │                              nginx ingress  (L7 routing)
        │                              TLS: cert-manager / Let's Encrypt
        │                                          │
        └────── HTTPS + CORS ────────────────►  ems-api  (1 replica)
                                               HTTP + SignalR
                                               Workers__Enabled=false
                                                       │
                                               ems-worker (1 replica)
                                               background jobs only, no ingress
                                               Workers__Enabled=true
                                                       │
                        ┌──────────────────────────────┴───────────────────┐
                        ▼                                                  ▼
          Azure PostgreSQL Flexible Server                        Azure Key Vault
          (B1ms, TLS enforced, 7-day PITR)              (Secrets Store CSI + workload identity)
```

Everything lives in one resource group, `rg-ems-prod`, in **southindia** (matches existing
resources and is the correct region for an IST application).

### Why managed Postgres, not a Postgres container

Running Postgres in-cluster means owning PersistentVolumes, backups, and restore. The
failure mode — a rescheduled pod with a misconfigured PVC losing the database, with no
backup — is unacceptable the week of an evaluation. Managed Flexible Server costs ~$17/mo
and provides automated backups with 7-day PITR, enforced TLS, and patching. The cluster
stays fully stateless.

### Why the API/worker split

`Program.cs` registers three hosted services:

```csharp
builder.Services.AddHostedService<BookingExpiryService>();
builder.Services.AddHostedService<EmailDispatcherService>();
builder.Services.AddHostedService<EventReminderService>();
```

There is no leader election. Every replica runs its own copy. At two or more API replicas,
`EmailDispatcherService` sends each queued email once per replica and `EventReminderService`
sends every attendee duplicate reminders.

The fix is to gate these behind a `Workers__Enabled` config flag and run two Deployments
from the **same container image**:

| Deployment   | Replicas | Ingress | `Workers__Enabled` |
|--------------|----------|---------|--------------------|
| `ems-api`    | 1        | yes     | `false`            |
| `ems-worker` | 1        | no      | `true`             |

At one API replica this bug is latent, not active — but the split costs ~200 MiB and means
scaling later is `kubectl scale` rather than a redesign and an incident.

## Sizing

`Standard_B2s` **is not offered in southindia** (verified via `az vm list-skus`); only the
v2 B-series is. The node pool uses **1 × `Standard_B2als_v2`** (2 vCPU, 4 GiB).

A 4 GiB node does not give 4 GiB to pods:

```
  4096 MiB   node RAM
- 1024 MiB   kube-reserved (25% of first 4 GiB)
-  750 MiB   hard eviction threshold
──────────
≈ 2300 MiB   allocatable
```

Budget against that ~2.3 GiB:

| Workload                                     | Memory     |
|----------------------------------------------|------------|
| AKS system pods (coredns, metrics-server, CSI, kube-proxy) | ~700 MiB–1 GiB |
| ingress-nginx                                | ~150 MiB   |
| cert-manager (3 pods)                        | ~150 MiB   |
| Secrets Store CSI + Azure provider           | ~100 MiB   |
| `ems-api` × 1                                | ~250 MiB   |
| `ems-worker` × 1                             | ~200 MiB   |
| **Total**                                    | **~1.6 GiB** |

Leaves ~700 MiB headroom. Enabling Container Insights (~300 MiB) or a second API replica
(~250 MiB) each erode that; doing both would not fit and pods would go `Pending` or be
OOMKilled. Hence: no Container Insights, one API replica.

Because there is one API pod, **SignalR needs no sticky sessions and no Redis backplane** —
every client necessarily lands on the same pod.

Deployments set `maxSurge: 0` so a rolling update does not need room for an extra pod on a
tight node. This costs a few seconds of downtime per deploy. Accepted.

## Cost

| Resource                          | SKU                  | $/mo    |
|-----------------------------------|----------------------|---------|
| AKS control plane                 | Free tier            | 0.00    |
| Node pool                         | 1 × `B2als_v2`       | 38.69   |
| Standard Load Balancer            | *(estimate)*         | ~18.00  |
| Standard static public IP         |                      | 3.65    |
| Container Registry                | Basic                | 5.00    |
| PostgreSQL Flexible Server        | B1ms + 32 GB *(est.)*| ~17.00  |
| Key Vault                         | Standard             | ~0.00   |
| Static Web App                    | Free                 | 0.00    |
| **Total**                         |                      | **≈ $82** |

VM and IP prices are verified against the Azure retail pricing API for southindia. Load
Balancer and Postgres are **estimates** — their meters did not surface cleanly from the API
and should be confirmed in the portal after provisioning.

For contrast, the equivalent App Service design is ~$25/mo. AKS costs roughly 3× here; it is
justified only because demonstrating Kubernetes + IaC is itself a goal of the capstone. This
runs on a shared corporate subscription (`Training-2026`) and the spend should be sanctioned.

The Load Balancer is not purely an ingress cost — AKS defaults to `outboundType:
loadBalancer` and uses it for egress SNAT (reaching Postgres, Stripe, Resend), so it exists
regardless. Avoiding it via node public IPs + `hostNetwork` was considered and **rejected**:
it ties the public IP to the node lifecycle, so any node repave changes the IP, silently
breaking DNS, the Let's Encrypt cert, and the Stripe webhook URL.

## Required code changes

The app will not run correctly on AKS without these.

1. **`Dockerfile`** — multi-stage (.NET 9 SDK → ASP.NET runtime), non-root user. One image
   serves `ems-api`, `ems-worker`, and the migration Job.
2. **`Workers__Enabled` flag** — gate the three `AddHostedService` calls.
3. **`UseForwardedHeaders`** — *critical.* Behind the ingress, `UseHttpsRedirection()` sees
   inbound requests as plain HTTP and issues a redirect loop, taking the API down entirely.
   Must trust `X-Forwarded-Proto` / `X-Forwarded-For` from the ingress.
4. **`/health` endpoint** — liveness and readiness probes require one. Readiness should check
   the database; liveness should not (a DB blip must not kill pods).
5. **Migrations as a Kubernetes Job** — nothing calls `db.Migrate()` today, so against an
   empty Azure database the app crashes on first query. The image gains a `--migrate` mode
   that applies migrations and exits. CI runs it as a Job and **waits for completion before
   rolling out pods**, so migrations never race across replicas.
6. **Serilog** — drop the file sink in production (the `logs/` directory is ephemeral in a
   container); console only.
7. **CORS** — `Cors:AllowedOrigins` is hardcoded to `localhost:4200`. Override with the
   Static Web Apps URL via `Cors__AllowedOrigins__0`.
8. **`environment.prod.ts`** — still contains `REPLACE_WITH_PRODUCTION_API_URL` and a
   `pk_live_` placeholder. Set the real API URL and the Stripe **test** publishable key.
9. **Stripe webhook** — the endpoint is `POST /api/stripe/webhook` (note: unversioned).
   Register the deployed URL in the Stripe dashboard and put the resulting signing secret in
   Key Vault.

`DataSeeder` is left enabled: seeded demo data is wanted, and the default password
(`Test@1234`) is acceptable for a demo. It would be a security hole in production.

## Secrets

Bicep provisions Key Vault with RBAC. AKS uses **workload identity** federated to a
Kubernetes ServiceAccount, and the **Secrets Store CSI driver** mounts secrets into pods and
syncs them to a Kubernetes Secret consumed as env vars. No secrets in git, in manifests, or
in GitHub.

Secrets stored: `db-connection-string`, `jwt-key`, `stripe-secret-key`,
`stripe-webhook-secret`, `resend-api-key`.

The connection string must include `SSL Mode=Require` — Azure Postgres enforces TLS.

All Stripe keys are **test-mode**. A demo booking must not be able to move real money.

## Networking

Postgres uses public access with a firewall rule pinned to the AKS egress IP (the Load
Balancer's outbound public IP). This is simpler than VNet integration and adequate, since
TLS is enforced regardless.

The ingress gets a Standard static public IP with an Azure DNS label, giving a stable
`ems-api-<suffix>.southindia.cloudapp.azure.com` hostname. cert-manager issues a free
Let's Encrypt certificate against it — no domain purchase needed. Stripe webhooks require a
valid TLS cert, which this satisfies.

## Bicep layout

`infra/main.bicep` plus one module per resource, so each is independently readable:

```
infra/
├── main.bicep
└── modules/
    ├── acr.bicep
    ├── aks.bicep
    ├── postgres.bicep
    ├── keyvault.bicep
    └── swa.bicep
```

## CI/CD

GitHub Actions authenticating via **OIDC federated credentials** — no service-principal
secret stored in GitHub.

| Workflow          | Trigger                                | Steps |
|-------------------|----------------------------------------|-------|
| `infra.yml`       | manual dispatch                        | `az deployment group create` against Bicep |
| `deploy-api.yml`  | push to `main` touching `EventManagementSystem/**` | `dotnet test` → build & push image to ACR (tagged with commit SHA, never `latest`) → run migration Job and wait → `kubectl apply` → wait for rollout |
| `deploy-web.yml`  | push to `main` touching `EMSAngular/**` | `ng build --configuration production` → deploy to Static Web Apps |

Images are tagged with the commit SHA so a rollout is reproducible and `kubectl rollout undo`
means something.

## Risks

| Risk | Mitigation |
|---|---|
| `UseForwardedHeaders` missed → redirect loop, API fully down | Called out as step 3; verify with a live `curl` after first deploy |
| Migration Job races with pod rollout | CI waits for Job completion before `kubectl apply` |
| Node memory exhaustion | Set explicit resource `requests`/`limits`; budget above leaves ~700 MiB headroom |
| Node upgrade causes downtime | Accepted for a demo; scale to 2 nodes if it becomes a problem |
| Postgres firewall blocks AKS after egress IP change | Egress IP is a static resource, so it is stable |
| Load Balancer / Postgres cost higher than estimated | Confirm actual meters in the portal after provisioning |
