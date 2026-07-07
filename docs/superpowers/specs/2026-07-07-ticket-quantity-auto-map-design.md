# Auto-map Ticket Quantity to Seat-Type Capacity

**Date:** 2026-07-07

## Problem

Organizers currently type a ticket type's `TotalQuantity` by hand. This can drift
from the real number of seats of that type in the venue and is redundant with data
the system already owns. The quantity should be derived automatically from the seat
type's capacity and must not be editable by the user — most visibly during event
creation.

## Principle

`TotalQuantity` stops being user input. The backend is the single source of truth:
a ticket type's quantity **is** the number of venue seats matching its `SeatType`.
Consequence: **one ticket type per seat type per screening** (if quantity is always
the full seat-type capacity, two ticket types sharing a seat type would each claim
all the seats and oversell).

"Seat-type capacity" is the venue-wide count of seats of that type
(`ISeatRepository.CountByVenueAndType(venueId, seatType)`), matching the existing
allocation logic and the fact that a screening exposes the full venue seat grid.

## Backend Changes

### DTOs (`EMSModelLibrary/DTOs/TicketTypeDTOs.cs`)
- Remove `TotalQuantity` from `CreateTicketTypeRequest`.
- Remove `TotalQuantity` from `UpdateTicketTypeRequest`.
- Keep `TotalQuantity` (and `AvailableQuantity`) on the read-only `TicketTypeDto` —
  still returned to clients.

### `TicketTypeService.Create`
1. Validate name / seatType / price. Drop the `totalQuantity <= 0` check.
2. Load screening → event → authorize organizer (unchanged).
3. Validate sale window (unchanged).
4. **Uniqueness:** if a ticket type with the same `SeatType` already exists for this
   screening, throw `ValidationException`
   ("A ticket type for seat type '{seatType}' already exists for this screening.").
5. Compute `capacity = CountByVenueAndType(venueId, seatType)`; if `0`, throw
   `ValidationException` ("No seats of type '{seatType}' exist in this venue.").
6. Set `TotalQuantity = AvailableQuantity = capacity`.

### `TicketTypeService.Update`
- `soldQuantity = tt.TotalQuantity - tt.AvailableQuantity`.
- If `request.SeatType != tt.SeatType`:
  - if `soldQuantity > 0` → `ValidationException`
    ("Cannot change seat type after tickets have sold.");
  - else: enforce uniqueness against sibling ticket types, recompute `capacity`,
    set `TotalQuantity = AvailableQuantity = capacity` (soldQuantity is 0 here).
- If `request.SeatType == tt.SeatType`: recompute `capacity` from seats, set
  `TotalQuantity = capacity` and `AvailableQuantity = capacity - soldQuantity`.
- Name, price, sale window, and `IsActive` remain editable in all cases.

### Retire `ValidateAllocation`
The old helper summed sibling quantities and checked venue total capacity. Under the
new rules (one ticket type per seat type, quantity == seat count) those checks are
redundant. Replace it with two focused helpers:
- capacity lookup (with the "no seats of type" guard), and
- a uniqueness check against sibling ticket types for the screening.

## Frontend Changes (`EMSAngular`)

### Models (`core/models/ticket-type.model.ts`)
- Remove `totalQuantity` from `CreateTicketTypeRequest` and `UpdateTicketTypeRequest`
  interfaces. Keep it on `TicketTypeDto`.

### `features/organizer/event-form`
- Remove the `totalQuantity` control from `categoryGroup()`.
- Remove the Quantity input from `event-form.component.html`.
- Remove `totalQuantity` from the `ticketTypeService.create(...)` call.
- Show the seat type's capacity as **read-only** text beside each category, derived
  from the already-loaded venue seats (count seats of the selected type).
- Prevent selecting the same seat type in more than one category (client mirror of
  the backend uniqueness rule); the seat-type dropdown for a row excludes types
  already chosen in other rows.

### `features/organizer/ticket-types`
- Remove the `totalQuantity` control and its template input.
- Convert the seat-type field from free text to a dropdown: load the screening's
  event → venue seats, populate distinct seat types, and exclude types already used
  by existing ticket types on this screening.

## Seeder (`EMSApplicationLayer/DataSeeder.cs`)
`DataSeeder` constructs `TicketType` entities directly. Set each seeded ticket type's
`TotalQuantity` and `AvailableQuantity` to the count of its matching seat list so the
seeded data obeys the new invariant (one per seat type, quantity == seat count).

## Tests

### `EMSTests/Services/TicketTypeServiceTests`
- Create sets `TotalQuantity`/`AvailableQuantity` to the seat-type capacity.
- Create rejects a duplicate seat type for the same screening.
- Create rejects a seat type with zero seats in the venue.
- Update recomputes quantity when seat type changes and no tickets are sold.
- Update rejects a seat-type change after tickets have sold.
- Update keeps `AvailableQuantity = capacity - sold` when seat type is unchanged.

### `event-form` spec
- The create call no longer includes `totalQuantity`.
- Choosing a seat type already used by another category is prevented.

## Out of Scope
- No change to booking, seat reservation, or payment flows.
- No change to how "capacity" is scoped (stays venue-wide seat count by type).
