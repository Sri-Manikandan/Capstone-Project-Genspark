# Database Changelog (Audit Trail) — Design

**Date:** 2026-07-06
**Status:** Approved

## Goal

Keep a persistent record of **who** made **what** change **when**, in a dedicated
database table. Capture is automatic — no per-service code — via an EF Core
`SaveChanges` override in `EventContext`. Every insert/update/delete performed
during an authenticated HTTP request produces a field-level audit row.

## Decisions (from brainstorming)

| Question | Decision |
|---|---|
| Capture mechanism | Automatic via EF Core `SaveChanges` override |
| Scope | All entities (recursion-guarded against `ChangeLog` itself) |
| Detail | Field-level diff (old → new per changed column) |
| Actor / non-user changes | Resolve from HTTP JWT user; **skip entirely** when no authenticated user |
| Read access | Admin-only paged `GET /api/v1/changelogs` |

## 1. Data model — `ChangeLog`

New entity `EMSModelLibrary/Models/ChangeLog.cs`:

| Column | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `EntityName` | string | e.g. `"Event"` (the CLR entity name) |
| `EntityKey` | string | primary-key value(s) of the changed row, as string |
| `Action` | string | `Create` / `Update` / `Delete` |
| `Changes` | string (jsonb) | field diff JSON, e.g. `{"Name":{"old":"A","new":"B"}}` |
| `UserId` | int | the actor's user id |
| `UserRole` | string? | actor's role from JWT claims |
| `CreatedAt` | DateTime | UTC timestamp of the change |

- `UserId` is a **plain indexed column, no FK/navigation** — the audit trail must
  survive deletion of the acting user.
- Registered as `DbSet<ChangeLog> ChangeLogs` in `EventContext`.
- Configured in `OnModelCreating`: `Changes` mapped to `jsonb`; indexes on
  `(EntityName, EntityKey)`, `CreatedAt`, and `UserId`.

`Action` constant values live in a small static class in the DAL
(`EMSDALLibrary`), mapping `EntityState.Added → "Create"`, `Modified → "Update"`,
`Deleted → "Delete"`.

## 2. Actor resolution — `ICurrentUserAccessor`

- Interface `ICurrentUserAccessor` defined in **EMSDALLibrary** (so the context can
  depend on it without depending on ASP.NET Core):
  ```csharp
  public interface ICurrentUserAccessor
  {
      int? GetUserId();      // null when no authenticated HTTP user
      string? GetUserRole();
  }
  ```
- Implementation `HttpContextCurrentUserAccessor` in **EMSApplicationLayer**, using
  `IHttpContextAccessor` + existing `ClaimsHelper`. Returns `null` when there is no
  `HttpContext` or no authenticated user (this drives the "skip system changes" rule).

## 3. Capture — `EventContext` `SaveChanges` override

Override both `SaveChanges()` and `SaveChangesAsync(...)`:

1. `ChangeTracker.DetectChanges()`.
2. Resolve `userId = _currentUser.GetUserId()`. **If null → call `base` and return
   immediately** (no audit rows written).
3. Iterate `ChangeTracker.Entries()`, skipping:
   - entries whose entity is `ChangeLog` (recursion guard),
   - entries in `Unchanged` / `Detached` state.
4. Build a pending audit record per entry:
   - **Create** — new values of all set columns (`old` = null); key may be
     temporary (DB-generated) at this stage.
   - **Update** — for each property where `IsModified`: `old` = OriginalValue,
     `new` = CurrentValue.
   - **Delete** — key captured; `old` = current values, `new` = null.
5. **Two-phase save** so DB-generated PKs land in `EntityKey`:
   - `result = await base.SaveChangesAsync(...)`.
   - Backfill any temporary key values on pending Create records from the now-saved
     entities.
   - Add the resulting `ChangeLog` rows to the set and `base.SaveChangesAsync(...)`
     a second time.
   - Return the first `result`.

Serialization of `Changes` uses `System.Text.Json`.

## 4. Wiring — `Program.cs`

- `builder.Services.AddHttpContextAccessor();`
- `builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();`
- Inject `ICurrentUserAccessor` into `EventContext` via its constructor (works with
  `AddDbContext`, which resolves constructor dependencies from DI).

## 5. Migration

Single EF Core migration `AddChangeLog` adding the `ChangeLogs` table and its
indexes:
```
dotnet ef migrations add AddChangeLog \
  --project EventManagementSystem/EMSDALLibrary \
  --startup-project EventManagementSystem/EMSApplicationLayer
```

## 6. Read API — Admin only

- DTO `ChangeLogResponseDto` (all `ChangeLog` fields; `CreatedAt` mapped UTC→IST via
  `MappingProfile` per existing datetime convention).
- `IChangeLogRepository` + `ChangeLogRepository` (extends `AbstractRepository`) with a
  paged query supporting optional filters: `entityName`, `userId`, `action`, plus
  `page` / `pageSize`, ordered by `CreatedAt` descending.
- `IChangeLogService` + `ChangeLogService` → maps entities to DTOs.
- `ChangeLogsController`: `GET /api/v1/changelogs` guarded by
  `[Authorize(Roles = "Admin")]`, returning a paged result.
- DI registrations for the new repository and service in `Program.cs`.

## 7. Consequence: skipped flows

By the "skip system changes" decision, these produce **no** audit rows (no
authenticated HTTP user at write time):
- `DataSeeder` (startup).
- `BookingExpiryService` (background).
- Stripe webhook writes (`StripeWebhookService`).
- Anonymous auth flows: register, login, refresh-token issuance/rotation.

This is expected behaviour, not a gap.

## 8. Testing

Existing suite is pure-unit (mocked repositories, real AutoMapper). The
`SaveChanges` override needs a real context, so add a focused test using the **EF
Core InMemory** provider with a fake `ICurrentUserAccessor`:

- Create → one `ChangeLog` row, `Action = Create`, diff has new values.
- Update of one field → one row, `Action = Update`, diff has only that field's
  old→new.
- Delete → one row, `Action = Delete`.
- Null actor (`GetUserId()` returns null) → **zero** `ChangeLog` rows.
- `ChangeLogService` unit tests (mocked repo) for filtering/paging + DTO mapping,
  in the existing style.

Add the `Microsoft.EntityFrameworkCore.InMemory` package to `EMSTests` if not
already present.

## Out of scope

- Frontend/Angular UI for viewing the changelog.
- Retention/archival/purge policy for old audit rows.
- Auditing of read operations.
