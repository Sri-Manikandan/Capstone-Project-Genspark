# Venue Screens, Add-Venue Dialog & Interactive Seat-Map Builder

**Date:** 2026-06-23
**Status:** Approved (design)

## Problem

The application currently models a venue as a single seating arrangement: a `Venue`
owns `Seat` rows directly, and an `Event` binds to a whole `Venue`. Real venues
(multiplexes, theatres, arenas) have **multiple screens/halls**, and a show runs in
**one** of them. There is also no interactive way for an admin to define a screen's
seats, and adding a venue uses an inline form at the bottom of the list page.

This work delivers three things:

1. Add/edit venue via a **modal dialog** instead of an inline form.
2. Model a venue as having **many screens (or a single one)**, with each event running
   in one chosen screen.
3. An **interactive grid-based seat-map builder** for defining a screen's seats.

## Approved Decisions

- **Screen model:** Reuse the existing `Seat.Section` field as the screen name. A
  "screen" is the set of seats in a venue sharing a `Section` value. No new table.
- **Event ↔ screen:** An event picks one screen. `Event` gains a non-null `Screen`
  string defaulting to `""` (= the `Section` value). Empty means "whole venue"
  (back-compat).
- **Builder style:** Grid generator + paint (style A). Type rows × seats-per-row to
  generate a grid, then click/drag seats to set type or carve aisles. No per-seat
  x/y coordinates — maps onto existing `Row` / `SeatNumber` / `SeatType` columns.
- **Editing a booked screen:** **Blocked.** If any seat in a screen is already booked,
  the save-screen operation is refused with a `ValidationException`.

## Out of Scope (YAGNI)

- Freeform drag-and-drop canvas / curved geometry (no x/y coordinate storage).
- A separate `Screen` entity/table.
- Repurposing or structuring `Venue.LayoutConfig` (left untouched, dropped from UI).
- Migrating existing seed seats into named screens (existing data keeps its current
  `Section` values; empty-`Screen` events keep showing all venue seats).

## Data Model

- **`Event.Screen`** — `string` (default `""`). Holds the screen name = `Seat.Section`.
  One EF Core migration: `AddEventScreen`.
- No schema change to `Seat` or `Venue`.

## Backend Changes (.NET)

### Event screen field
- Add `Screen` to `EventDto`, `CreateEventRequest`, `UpdateEventRequest`.
- `MappingProfile`: map `Screen` straight through (plain string — no UTC→IST handling).
- `EventService` create/update: persist `Screen`.

### Screen-scoped seat availability
- `SeatRepository.GetAvailableByEventId`: when `event.Screen` is non-empty, add
  `&& s.Section == event.Screen` to the seat query. Empty `Screen` keeps current
  whole-venue behaviour.

### Save-screen endpoint (new)
- DTO `SetScreenSeatsRequest { int VenueId; string Screen; List<ScreenSeatDto> Seats; }`
  where `ScreenSeatDto { string Row; int SeatNumber; string SeatType; }`.
- `ISeatService.SetScreenSeats(SetScreenSeatsRequest)`:
  1. Load existing seats for `VenueId` + `Section == Screen`.
  2. **Guard:** if any of those seats is booked (appears in a non-`Cancelled`
     `BookingItem`/`Booking` for any event), throw `ValidationException`
     ("Cannot edit a screen that already has bookings.").
  3. Otherwise delete the existing seats for that venue+section and insert the new
     list, all in one transaction.
- Endpoint: `PUT /api/v1/seat/screen`, `[Authorize(Roles = "Admin")]`.
- The existing rigid `POST /api/v1/seat/bulk` (single contiguous row, single type)
  stays for backward compatibility.

## Frontend Changes (Angular)

### 1. Venue add/edit dialog
- Replace the inline form at the bottom of `AdminVenuesComponent` with an
  **"+ Add venue"** button that opens a **modal dialog**; **Edit** opens the same
  dialog pre-filled.
- Fields: name, city, address, capacity. `layoutConfig` dropped from the form and
  defaulted (`"{}"`) in the request.
- Reuse an existing modal/overlay component if present in `shared/components`;
  otherwise add a small reusable `ems-modal` shell there.

### 2. Interactive seat builder (`/admin/venues/:id/seats`)
- Page becomes a **screen manager + builder**:
  - **Screen list** — the venue's distinct `Section` values, with **"+ Add screen"**
    (prompt for a name). Selecting a screen loads its seats into the builder.
  - **Builder canvas**:
    - Inputs: *rows* and *seats-per-row* → **Generate** an editable grid.
    - **Paint palette** of seat types (Normal, Premium, + add custom type name).
    - Click or drag a seat to apply the active type, or toggle it to an
      **aisle/empty** (omitted from the saved list).
    - Row labels auto-assigned A, B, C, … ; seat numbers 1..n per row.
    - **Save** → `PUT /api/v1/seat/screen` with the full `{venueId, screen, seats[]}`.
  - Styling consistent with the existing `SeatMapComponent`.

### 3. Event form & booking
- Event create/edit form gains a **Screen dropdown**, populated from the selected
  venue's screens (derived client-side from the venue's seats — distinct `Section`s).
- Booking seat-map: no logic change (backend already scopes seats to the screen).
  The "Stage / Screen" header displays the event's screen name when present.

### Models / services
- `core/models/seat.model.ts`: add `SetScreenSeatsRequest` / `ScreenSeat` interfaces.
- `core/models/event.model.ts`: add `screen` to event DTO + create/update requests.
- `SeatService`: add `setScreenSeats(req)`; helper to derive a venue's screens.

## Testing

### Backend (NUnit + Moq + FluentAssertions)
- `SeatServiceTests`:
  - `SetScreenSeats` replaces existing seats for the venue+section.
  - `SetScreenSeats` throws `ValidationException` when a seat in the screen is booked.
  - Availability filters by `Section` when `event.Screen` is set; returns all venue
    seats when `Screen` is empty.

### Frontend (Jasmine/Karma)
- Seat-builder spec: grid generation produces the expected rows × seats, aisles are
  excluded, and the save payload matches `{venueId, screen, seats[]}`.
- Venue dialog spec: dialog opens for add and edit, and submit calls the right
  create/update service method.

## Migration / Rollout Notes

- Run `dotnet ef migrations add AddEventScreen` then `database update`.
- Existing events keep `Screen = ""` → unchanged whole-venue behaviour.
- Existing seed seats keep their current `Section` values; admins can rename/rebuild
  screens via the builder where no bookings exist.
