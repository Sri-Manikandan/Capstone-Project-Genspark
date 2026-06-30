# List Filters + Signal Store — Design

Date: 2026-06-30
Status: Approved

## Problem

List pages in the Angular frontend expose little or no filtering. The public
event browse page (`features/events/event-list`) shows only two plain text
inputs (query, category) even though the API already supports query, category,
status, date range, and sorting. Other server-backed lists (my bookings, admin
users, admin organizer-requests) have no filter UI at all. There is no shared,
reusable pattern for filter state, so each list would otherwise reinvent it.

## Goals

- A reusable, type-safe signal-based filter store pattern.
- Filter UI on every list page that has server-side filter support.
- Filter state held in memory and preserved across navigation (in-memory signal
  store, singleton per feature). URL sync is explicitly out of scope.

## Non-goals (deferred follow-ups)

- URL / query-param synchronization of filters.
- Filtering on **organizer events** (`features/organizer/event-list`) and
  **admin venues** (`features/admin/venues`). Their services (`getMyEvents`,
  `VenueService.list`) return all rows with no server-side filter support;
  adding filters there needs backend work or client-side-only filtering and is
  left for a later spec.

## Architecture

### Shared helper — `core/state/filter-store.ts`

A generic factory providing the common mechanics. No Angular dependency beyond
signals; one concept per file.

```ts
export interface FilterStore<T extends object> {
  filters: Signal<T>;
  page: WritableSignal<number>;
  pageSize: number;
  patch(partial: Partial<T>): void;   // merge into filters AND reset page to 1
  setPage(p: number): void;           // change page only (pagination)
  reset(): void;                      // restore initial filters + page 1
}

export function createFilterStore<T extends object>(config: {
  initial: T;
  pageSize: number;
}): FilterStore<T>;
```

Behaviour:
- `filters` starts as a copy of `config.initial`.
- `patch` shallow-merges the partial into the current filters and sets
  `page` back to 1 (any filter change returns to the first page).
- `setPage` sets the page without touching filters.
- `reset` restores a fresh copy of `config.initial` and sets page to 1.

### Per-feature stores

Each list gets a thin, fully-typed `@Injectable({ providedIn: 'root' })` store
that composes `createFilterStore` and exposes a `request` computed mapping to
the API request DTO. `providedIn: 'root'` makes the store a singleton, so
filter state survives navigation away and back.

Example:

```ts
// features/events/event-filter.store.ts
export interface EventFilters {
  query: string;
  category: string;
  sortBy: 'startTime' | 'title' | 'createdAt';
  sortOrder: 'asc' | 'desc';
  startFrom: string;   // '' when unset
  startTo: string;     // '' when unset
}

@Injectable({ providedIn: 'root' })
export class EventFilterStore {
  private store = createFilterStore<EventFilters>({
    initial: { query: '', category: '', sortBy: 'startTime', sortOrder: 'asc', startFrom: '', startTo: '' },
    pageSize: 9,
  });
  readonly filters = this.store.filters;
  readonly page = this.store.page;
  readonly categories = signal<string[]>([]);
  readonly request = computed<EventSearchRequest>(() => {
    const f = this.store.filters();
    return {
      query: f.query || undefined,
      category: f.category || undefined,
      startFrom: f.startFrom || undefined,
      startTo: f.startTo || undefined,
      sortBy: f.sortBy,
      sortOrder: f.sortOrder,
      page: this.store.page(),
      pageSize: this.store.pageSize,
    };
  });
  patch = this.store.patch;
  setPage = this.store.setPage;
  reset = this.store.reset;
}
```

Note: the public events page does **not** expose a `status` filter — the
controller forces `Status = Published` for non-admins server-side.

### Component wiring

Components keep their own `events`/`loading`/`error`/`totalPages` signals
(results + request state stay in the component, not the store — single
responsibility). The list reloads reactively:

```ts
constructor() {
  effect(() => {
    const req = this.store.request();   // tracked dependency
    this.load(req);
  });
}
```

- Text inputs (search) debounce ~300ms via the reactive form control's
  `valueChanges` → `store.patch({ query })`.
- Dropdowns and date pickers apply immediately via `store.patch`.
- Pagination calls `store.setPage`.
- A "Clear filters" button calls `store.reset`.

Filter bars are hand-written per list using existing Tailwind primitives; no
shared `<ems-filter-bar>` component.

### Backend change — categories endpoint

For the events category dropdown, add `GET /api/v1/Event/categories` returning
distinct categories of Published events as `string[]`.

- `EMSDALLibrary/Repositories/EventRepository.cs` — `GetCategories()`:
  `Events.Where(Status == Published).Select(Category).Distinct().OrderBy(c => c)`.
- `IEventRepository` — add signature.
- `EMSBLLLibrary/Interfaces/IEventService.cs` + `EventService.cs` —
  `GetCategories()` passthrough.
- `EventController.cs` — `[HttpGet("categories")]`, anonymous, returns `Ok(list)`.
- Angular `EventService.getCategories(): Observable<string[]>`.

`EventStatus.Published` constant already exists in `EMSBLLLibrary/Constants`.

## Per-list rollout (phases)

- **Phase 0** — `createFilterStore` helper + spec.
- **Phase 1 — Events** — categories endpoint (backend + Angular service),
  `EventFilterStore`, rebuilt filter bar (debounced search, category dropdown,
  sort by + order, date range, Clear), `effect()` wiring. Update
  `event-list.component.spec.ts`.
- **Phase 2 — My bookings** (`features/bookings/booking-list`) —
  `BookingFilterStore` over `BookingQueryRequest` (status only); status
  dropdown + Clear.
- **Phase 3 — Admin users** (`features/admin/users`) — `UserFilterStore` over
  `UserSearchRequest`: debounced query, role dropdown, active/inactive select.
- **Phase 4 — Admin organizer-requests** (`features/admin/organizer-requests`)
  — store over `OrganizerRequestQueryRequest` (status); status dropdown.

## Testing

- Karma spec for `createFilterStore`: `patch` merges + resets page to 1,
  `setPage` leaves filters, `reset` restores initial + page 1.
- Karma spec per per-feature store verifying the `request` computed shape
  (empty strings become `undefined`).
- Update `event-list.component.spec.ts` for the new store-driven flow.
- NUnit test for `EventService.GetCategories()` (distinct, published-only).

## Files touched (summary)

New (Angular):
- `core/state/filter-store.ts` (+ spec)
- `features/events/event-filter.store.ts` (+ spec)
- `features/bookings/booking-filter.store.ts` (+ spec)
- `features/admin/users/user-filter.store.ts` (+ spec)
- `features/admin/organizer-requests/organizer-request-filter.store.ts` (+ spec)

Modified (Angular):
- `core/services/event.service.ts` (getCategories)
- `features/events/event-list/*` (ts/html/spec)
- `features/bookings/booking-list/*`
- `features/admin/users/*`
- `features/admin/organizer-requests/*`

Modified (backend):
- `EMSDALLibrary/Repositories/EventRepository.cs`, `IEventRepository`
- `EMSBLLLibrary/Interfaces/IEventService.cs`, `Services/EventService.cs`
- `EMSApplicationLayer/Controllers/EventController.cs`
- `EMSTests/Services/EventServiceTests` (categories test)
