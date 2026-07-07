# Event Lead-Time Rule & Richer Admin Approvals — Design

**Date:** 2026-07-07
**Status:** Approved (pending spec review)

Two related changes to the event lifecycle:

1. **48-hour minimum lead time** — an event's start must be at least 48 hours away, enforced on both create and edit, with a visible disclaimer to organizers.
2. **Richer admin approval view** — give the admin the organizer's profile, track record, full event details, and automated review signals so they can make an informed approve/reject decision.

---

## Feature 1 — 48-Hour Minimum Lead Time

### Rule

An event's `StartTime` must be **≥ 48 hours after the moment of submission**. Enforced on **create and edit/reschedule**, for **all roles** (no admin exemption). Measured as a strict duration: `StartTime ≥ UtcNow + 48h`.

### Backend (source of truth) — `EMSBLLLibrary/Services/EventService.cs`

- Add a private constant: `private static readonly TimeSpan MinLeadTime = TimeSpan.FromHours(48);`
- **`Create`**: replace the existing check
  ```csharp
  if (startUtc <= DateTime.UtcNow)
      throw new ValidationException("StartTime must be in the future.");
  ```
  with
  ```csharp
  if (startUtc < DateTime.UtcNow + MinLeadTime)
      throw new ValidationException("Events must be scheduled at least 2 days (48 hours) in advance.");
  ```
- **`Update`**: currently enforces **no** future check on `StartTime` at all. Add the same lead-time check on the new start time (after the `endUtc <= startUtc` check). Only `Draft` / `Rejected` events are editable (Update rejects `PendingApproval` and `Published`), so this only constrains reschedules of not-yet-live events.

### Frontend — `EMSAngular/src/app/features/organizer/event-form/`

- **New validator** in `shared/validators/form-validators.ts`:
  ```ts
  /** Requires a datetime at least `hours` in the future. Mirrors the create/edit lead-time check. */
  export function minLeadTime(hours: number): ValidatorFn { ... } // surfaces { minLeadTime: { hours } }
  ```
  Returns `null` for empty/unparseable values (lets `required` own those), else `{ minLeadTime: { hours } }` when `when < Date.now() + hours*3600_000`.
- Apply `minLeadTime(48)` to the `startTime` control on **both** create and edit. This replaces the current `futureDateTime` usage on this form. In `ngOnInit`, the edit branch currently downgrades `startTime` to just `[Validators.required]` — change it to `[Validators.required, minLeadTime(48)]` so edits enforce the rule too.
- **`FieldErrorComponent`** — add a message mapping for `minLeadTime`: `"Must be at least 2 days (48 hours) from now."`
- `futureDateTime` stays in the file (it may be used elsewhere / kept for reuse); only this form's usage switches to `minLeadTime`.

### Disclaimer

A visible callout in `event-form.component.html`, placed near the start-time field, shown for both create and edit:

> 📅 **Heads up:** Events must be scheduled **at least 2 days (48 hours) in advance**. Plan submissions accordingly — last-minute events can't be published, and this window also gives admins time to review and approve your event.

Styled as an informational note consistent with the existing form styling (not an error state).

### Tests

- **`EventServiceTests`**:
  - `Create` rejects a start < 48h away (e.g. +24h) with `ValidationException`; accepts ≥ 48h (e.g. +72h).
  - `Update` rejects a reschedule to < 48h away; accepts ≥ 48h.
  - Existing create/update happy-path tests updated to use start times ≥ 48h out so they keep passing.
- **`event-form.component.spec.ts`**: form invalid when start is < 48h out; valid when ≥ 48h; disclaimer renders.

---

## Feature 2 — Richer Admin Approval View

### Approach

Enrich the **existing** `GET /events/pending` response into a dedicated `PendingEventReviewDto`. One call returns everything the admin screen needs. Rationale over alternatives:

- **vs. a second endpoint** — the pending list is the only consumer of this data; folding it in keeps the surface small.
- **vs. frontend making per-event lookups** (organizer, venue, tickets) — that's N+1 chatty calls from the browser and duplicates join logic client-side. The BLL already has the repositories to assemble this server-side.

Approve/reject endpoints and their DTOs are unchanged. Only the pending-list shape grows.

### New DTO — `EMSModelLibrary/DTOs/EventDTOs.cs`

```csharp
public class PendingEventReviewDto
{
    // Event core (existing EventDto fields)
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string ImageUrl { get; set; }
    public DateTime StartTime { get; set; }   // IST
    public DateTime EndTime { get; set; }      // IST
    public string Screen { get; set; }
    public DateTime CreatedAt { get; set; }    // IST — when submitted

    // Venue
    public string VenueName { get; set; }
    public string City { get; set; }

    // Organizer profile
    public OrganizerSummaryDto Organizer { get; set; }

    // Ticket categories offered
    public List<TicketCategorySummaryDto> TicketCategories { get; set; }

    // Automated review signals
    public ReviewSignalsDto Signals { get; set; }
}

public class OrganizerSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public DateTime MemberSince { get; set; }  // IST — User.CreatedAt
    public bool IsActive { get; set; }
    // Track record
    public int PublishedEventCount { get; set; }
    public int RejectedEventCount { get; set; }
    public int TotalEventCount { get; set; }
}

public class TicketCategorySummaryDto
{
    public string Name { get; set; }
    public string SeatType { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
}

public class ReviewSignalsDto
{
    public bool LeadTimeOk { get; set; }        // start ≥ 48h from submission
    public bool ImageUrlValid { get; set; }     // well-formed absolute http(s) URL (format only)
    public bool DescriptionAdequate { get; set; } // length ≥ 30 chars
    public bool HasTicketCategories { get; set; }
    public bool PricingSane { get; set; }        // every category price > 0
}
```

### Data assembly — `EventService.GetPendingApproval()`

Returns `List<PendingEventReviewDto>`. For each pending event:

1. Map core event fields + venue (reuse existing venue-lookup pattern).
2. Load the organizer `User` via `IUserRepository` (newly injected into `EventService`).
3. Compute the organizer's track record via a new repo method (below).
4. Load ticket categories: `IScreeningRepository.GetByEventId` → for each screening, `ITicketTypeRepository.GetByScreeningId` → flatten to `TicketCategorySummaryDto`. (Both repos are already available; inject if not already.)
5. Compute `ReviewSignalsDto`:
   - `LeadTimeOk` = `event.StartTime (UTC) >= event.CreatedAt (UTC) + 48h` — i.e. was it valid lead-time when submitted. (Uses stored UTC values before IST mapping.)
   - `ImageUrlValid` = format-only check: parseable absolute URL with `http`/`https` scheme. **No network fetch** — a live HEAD per event would make the admin list slow and flaky.
   - `DescriptionAdequate` = `Description.Trim().Length >= 30`.
   - `HasTicketCategories` = any categories found.
   - `PricingSane` = categories exist and all prices > 0.

All datetime fields (`StartTime`, `EndTime`, `CreatedAt`, `MemberSince`) go through `TimeHelper.UtcToIst` per the repo mapping rule — applied in the mapping profile or explicitly during assembly.

### New repository method

`IEventRepository.GetStatusCountsByOrganizer(int organizerId)` → returns counts sufficient to fill published / rejected / total. Implementation in `EventRepository` groups the organizer's events by `Status`:

```csharp
Task<(int Published, int Rejected, int Total)> GetStatusCountsByOrganizer(int organizerId);
```

Uses `EventStatus.Published` / `EventStatus.Rejected` constants.

### Controller

`GET /events/pending` (Admin-only) now returns `List<PendingEventReviewDto>`. No route/verb/auth change.

### Frontend — `EMSAngular/src/app/features/admin/event-approvals/`

- **Model** (`core/models/`): add `PendingEventReview` interface mirroring the DTO (+ nested `OrganizerSummary`, `TicketCategorySummary`, `ReviewSignals`).
- **`AdminService.getPendingEvents()`**: change return type to `Observable<PendingEventReview[]>`.
- **Component**: `events` signal typed to the new interface. Approve/reject logic unchanged.
- **Template redesign** — each pending event becomes a richer card with sections:
  - **Header**: title, category, submitted-on, start→end times, venue + city, image preview thumbnail.
  - **Organizer panel**: name, email, phone, member-since, active badge; track-record chips (Published / Rejected / Total).
  - **Ticket categories**: compact table (name, seat type, price, capacity).
  - **Review signals**: a green ✓ / red ✗ checklist rendered from the `signals` object.
  - **Actions**: existing reason input + Approve / Reject buttons, unchanged behavior.

### Tests

- **`EventServiceTests`**: `GetPendingApproval` returns enriched DTOs — organizer profile populated, track-record counts correct, ticket categories flattened, and each signal computed correctly (both true and false cases for at least lead-time, image, description, pricing). Mock `IUserRepository`, `IScreeningRepository`, `ITicketTypeRepository`, and the new count method.
- **`event-approvals.component.spec.ts`**: renders organizer info, track-record chips, ticket table, and signal checklist from a mocked enriched response.

---

## Files Touched (summary)

**Backend**
- `EMSBLLLibrary/Services/EventService.cs` — lead-time checks (create + update); enrich `GetPendingApproval`; inject `IUserRepository`, `IScreeningRepository`*, `ITicketTypeRepository`.
- `EMSModelLibrary/DTOs/EventDTOs.cs` — new DTOs.
- `EMSDALLibrary/Interfaces/IEventRepository.cs` + `Repositories/EventRepository.cs` — `GetStatusCountsByOrganizer`.
- `EMSBLLLibrary/Mappings/MappingProfile.cs` — mapping for new DTOs if used (UTC→IST).
- `EMSApplicationLayer/Controllers/EventController.cs` — pending endpoint return type.
- `EMSTests/Services/EventServiceTests.cs` — new + updated tests.

\* `IScreeningRepository` is already injected into `EventService`; `IUserRepository` and `ITicketTypeRepository` are new injections.

**Frontend**
- `shared/validators/form-validators.ts` — `minLeadTime`.
- `shared/components/field-error/field-error.component.ts` — error message.
- `features/organizer/event-form/event-form.component.ts` + `.html` — validator wiring + disclaimer.
- `core/models/` — `PendingEventReview` and nested interfaces.
- `core/services/admin.service.ts` — return type.
- `features/admin/event-approvals/event-approvals.component.ts` + `.html` — enriched view.
- Corresponding `.spec.ts` files.

## Out of Scope / YAGNI

- No network reachability check on image URLs (format-only).
- No admin bypass of the lead-time rule.
- No change to approve/reject endpoints or their DTOs.
- No pagination change on the pending list (stays a plain array).
