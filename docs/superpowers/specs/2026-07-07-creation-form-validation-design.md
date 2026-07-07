# Creation-form validation alignment — Design

**Date:** 2026-07-07
**Scope:** Angular frontend only (`EMSAngular/`). No backend changes.

## Goal

Make the four creation forms — **venue**, **event**, **ticket type**, **screening** —
validate every field client-side in a way that exactly mirrors the backend rules, and
surface backend rejections correctly. The result: users hit avoidable server round-trips
far less often, and any rule the client cannot pre-check still shows the backend's own
message.

## Background: how errors already flow

The error-reporting path is already correct and is **not** being changed:

```
Service throws ValidationException/NotFoundException
  → ExceptionMiddleware serializes { error: message }  (camelCase JSON)
  → extractError() (core/services/http-error.ts) reads body.error
  → component surfaces it via ToastService (venue, event) or an error() signal + <ems-alert> (ticket-types, screenings)
```

DataAnnotation failures from `[ApiController]` return `ValidationProblemDetails`
(`{ errors: { field: [msg] } }`), which `extractError` also handles. So the work is
purely about adding **client-side validators that mirror the backend**, plus the matching
inline messages.

## Backend rules being mirrored

| Entity | Field | Backend rule (DTO annotation + service) |
|---|---|---|
| Venue | Name | required, 2–100, non-blank |
| Venue | Address | required, 5–500, non-blank |
| Venue | City | required, 2–100, non-blank |
| Venue | TotalCapacity | 1–100000 |
| Event | Title | required, 2–200, non-blank |
| Event | Description | required, 1–2000, non-blank |
| Event | StartTime | required, **≥ now + 48h** (create *and* update) |
| Event | EndTime | required, **> StartTime** |
| Event | ImageUrl | required, absolute http(s) URL |
| Event | Category | required |
| Event (inline ticket) | Name | required, 2–100 |
| Event (inline ticket) | SeatType | required |
| Event (inline ticket) | Price | 0–100000 |
| TicketType | Name | required, 2–100 |
| TicketType | SeatType | required (from dropdown) |
| TicketType | Price | 0–100000 |
| TicketType | SaleStart/SaleEnd | required, SaleEnd > SaleStart, **SaleEnd ≤ screening start** |
| Screening | Screen | required, 1–50 |
| Screening | StartTime | required, **in the future**, ≥ event.StartTime |
| Screening | EndTime | required, > StartTime, ≤ event.EndTime |

## New shared validators — `shared/validators/form-validators.ts`

- `notBlank(control)` → `{ notBlank: true }` when the value is non-empty but
  whitespace-only. Returns `null` for an empty value so `Validators.required` owns that
  case. Mirrors `IsNullOrWhiteSpace`.
- `minLeadTime(hours: number): ValidatorFn` → `{ leadTime: { hours } }` when the datetime
  is earlier than `now + hours`. Empty/NaN → null (let `required` own it). Mirrors the 48h
  event rule.
- `notBefore(boundFn: () => string | null | undefined, key: string): ValidatorFn` →
  `{ [key]: true }` when the control's datetime is earlier than the bound. `boundFn` reads
  a signal, so the control revalidates once the bound loads.
- `notAfter(boundFn: () => string | null | undefined, key: string): ValidatorFn` → mirror
  of `notBefore` for upper bounds.

All datetime comparisons parse the `datetime-local` wall-clock string with `new Date(...)`,
consistent with the existing `futureDateTime`/`endAfterStart` validators, and treat empty
or unparseable values as "no opinion" (return null).

## Error messages — `field-error.component.ts`

Add cases (wording matched to the backend where a backend message exists):

- `notBlank` → `${label} cannot be blank.`
- `leadTime` → `Must be scheduled at least 48 hours in advance.`
- `saleAfterScreening` → `Ticket sales must end before the screening starts.`
- `outsideEventWindow` → `Must fall within the event's start and end times.`

## Per-form changes

### Venue — `features/admin/venues/admin-venues.component.ts`
Add `notBlank` to `name`, `address`, `city`. (Ranges/lengths/capacity already match.)

### Event — `features/organizer/event-form/event-form.component.ts`
- `startTime` validators become `[Validators.required, minLeadTime(48)]` as the form
  default, applied on **both** create and edit.
- **Remove** the `ngOnInit` edit-branch override that stripped the future check, and remove
  its now-incorrect comment (the API *does* enforce the 48h rule on update).
- Add `notBlank` to `title` and `description`.
- Inline ticket `price` control gains `Validators.max(100000)`.
- Existing group-level `endAfterStart('startTime','endTime')` is retained (covers edit too).

### Ticket type — `features/organizer/ticket-types/ticket-types.component.ts`
- `name` gains `Validators.maxLength(100)`.
- `price` becomes `[Validators.required, Validators.min(0), Validators.max(100000)]`.
- Form gains group-level `endAfterStart('saleStart','saleEnd')`.
- Add a `screeningStart` signal, set from the already-fetched screening (`s.startTime`,
  sliced to `YYYY-MM-DDTHH:mm`). Attach `notAfter(() => screeningStart(), 'saleAfterScreening')`
  to `saleEnd`; call `updateValueAndValidity()` when the screening loads.

### Screening — `features/organizer/screenings/screenings.component.ts`
- `screen` gains `Validators.maxLength(50)`.
- Form gains group-level `endAfterStart('startTime','endTime')`.
- `startTime` gains `futureDateTime`.
- Inject `EventService`; fetch the event (`eventId` route param) and store `eventStart` /
  `eventEnd` signals (sliced to `YYYY-MM-DDTHH:mm`). Attach
  `notBefore(() => eventStart(), 'outsideEventWindow')` to `startTime` and
  `notAfter(() => eventEnd(), 'outsideEventWindow')` to `endTime`; revalidate when loaded.

## Testing

- New unit tests for `notBlank`, `minLeadTime`, `notBefore`, `notAfter` in the
  form-validators spec.
- Update existing component specs (`event-form`, `admin-venues`) for the new validators;
  add focused checks to `ticket-types` and `screenings` specs where present.

## Verification

- `cd EMSAngular && ng test` (runs via Vitest per project setup).
- `cd EMSAngular && ng build`.
- No backend build/test needed — backend is untouched.

## Out of scope

- Backend rule changes (the 48h-on-edit rule stays; the frontend mirrors it).
- Any form other than the four creation forms above.
- Restyling / layout changes.
