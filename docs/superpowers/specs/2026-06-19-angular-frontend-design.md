# Angular Frontend Design — Event Management System

**Date:** 2026-06-19  
**Backend:** ASP.NET Core API at `EventManagementSystem/`  
**Frontend location:** `EMSAngular/` (sibling to `EventManagementSystem/`)

---

## Overview

A single Angular application serving three roles — **User**, **Organizer**, and **Admin** — against the existing EMS REST API. The app uses feature modules with lazy loading, Angular Signals for state, Tailwind CSS for styling, Stripe Payment Element for checkout, and SignalR for real-time seat updates.

---

## Section 1: Project Structure & Module Layout

```
EMSAngular/src/app/
├── core/
│   ├── guards/
│   │   ├── auth.guard.ts
│   │   └── role.guard.ts
│   ├── interceptors/
│   │   ├── jwt.interceptor.ts
│   │   └── auth-error.interceptor.ts
│   ├── models/
│   │   ├── event.model.ts
│   │   ├── booking.model.ts
│   │   ├── user.model.ts
│   │   ├── venue.model.ts
│   │   ├── seat.model.ts
│   │   ├── ticket-type.model.ts
│   │   ├── payment.model.ts
│   │   └── admin.model.ts
│   └── services/
│       ├── auth.service.ts
│       ├── event.service.ts
│       ├── booking.service.ts
│       ├── payment.service.ts
│       ├── seat.service.ts
│       ├── ticket-type.service.ts
│       ├── venue.service.ts
│       ├── user.service.ts
│       ├── admin.service.ts
│       └── seat-hub.service.ts
│
├── features/
│   ├── auth/            AuthModule   — login, register, forgot/reset password
│   ├── events/          EventsModule — public browse, event detail, seat map, checkout
│   ├── bookings/        BookingsModule — my bookings list, booking detail + QR
│   ├── organizer/       OrganizerModule — my events, create/edit, ticket types, QR scanner
│   └── admin/           AdminModule  — users, venues, seats, event approvals, organizer requests
│
├── shared/
│   ├── components/
│   │   ├── navbar/
│   │   ├── event-card/
│   │   ├── seat-map/
│   │   ├── stripe-payment/
│   │   ├── booking-qr/
│   │   ├── pagination/
│   │   ├── loading-spinner/
│   │   └── alert/
│   ├── directives/      (as needed)
│   └── pipes/
│       ├── ist-date.pipe.ts
│       └── currency-inr.pipe.ts
│
├── app.module.ts              (imports CoreModule, SharedModule, AppRoutingModule)
├── app-routing.module.ts      (lazy-loads all 5 feature modules)
└── app.component.ts           (shell: navbar + <router-outlet>)
```

`CoreModule` provides all singleton services and interceptors; it guards against re-import. `SharedModule` declares and exports all shared components and pipes; it is imported by every feature module.

---

## Section 2: Routing & Role-Based Access

### Route Map

```
/                                    → redirects to /events
/events                              → EventsModule (public)
/events/:slug                        → event detail
/auth/login                          → AuthModule
/auth/register
/auth/forgot-password
/auth/reset-password

— AuthGuard (must be logged in) —
/bookings                            → BookingsModule: my bookings list
/bookings/:id                        → booking detail + QR
/checkout/:bookingId                 → Stripe checkout page

— AuthGuard + RoleGuard (Organizer or Admin) —
/organizer/events                    → OrganizerModule: my events list
/organizer/events/new                → create event
/organizer/events/:id/edit           → edit event
/organizer/events/:id/tickets        → manage ticket types
/organizer/events/:id/bookings       → event bookings + QR scanner

— AuthGuard + RoleGuard (Admin only) —
/admin/users                         → AdminModule: user list + search
/admin/venues                        → venue list
/admin/venues/:id/seats              → seat management
/admin/events                        → pending event approval queue
/admin/organizer-requests            → organizer request queue

/404                                 → NotFoundComponent (catch-all)
```

### Guards

**`AuthGuard`** — reads `AuthService.isAuthenticated()` signal. If false, redirects to `/auth/login` with a `returnUrl` query param.

**`RoleGuard`** — reads `AuthService.role()` signal and compares against `data.roles` on the route. If the role doesn't match, redirects to `/`.

### Interceptors

**`JwtInterceptor`** — clones every outbound request and attaches `Authorization: Bearer <accessToken>` from `localStorage`.

**`AuthErrorInterceptor`** — catches 401 responses. Attempts a silent refresh via `AuthService.refresh()`. If the refresh succeeds, retries the original request once. If it fails, calls `AuthService.logout()` and navigates to `/auth/login`.

---

## Section 3: State & Services

All reactive state is managed with Angular Signals inside injectable services. No global store.

### `AuthService`

```ts
currentUser = signal<UserDto | null>(null)
isAuthenticated = computed(() => !!this.currentUser())
role = computed(() => this.currentUser()?.role ?? null)
```

On init, reads `accessToken` from `localStorage`, decodes expiry, and populates `currentUser`. Token pair (`accessToken`, `refreshToken`) persisted in `localStorage`.

Methods: `login()`, `register()`, `logout()`, `refresh()`, `forgotPassword()`, `resetPassword()`

### Domain Services

Each service exposes typed methods that return `Observable<T>`. Errors are caught and re-thrown as plain `string` messages extracted from the backend `{ message }` error body.

| Service | Key Methods |
|---|---|
| `EventService` | `search()`, `getById()`, `getBySlug()`, `getMyEvents()`, `create()`, `update()`, `delete()`, `submit()`, `cancel()` |
| `BookingService` | `create()`, `getById()`, `getByReference()`, `getMyBookings()`, `cancel()`, `validateQr()` |
| `PaymentService` | `initiate(bookingId, currency)` → `{ clientSecret }`, `confirm(paymentIntentId)` |
| `SeatService` | `getByVenue()`, `getAvailableByEvent()`, `reserve()`, `releaseReservation()` |
| `TicketTypeService` | `getByEvent()`, `getActiveByEvent()`, `create()`, `update()`, `delete()` |
| `VenueService` | `list()`, `getById()`, `create()`, `update()`, `delete()` |
| `AdminService` | `getOrganizerRequests()`, `approveOrganizerRequest()`, `rejectOrganizerRequest()`, `getPendingEvents()`, `approveEvent()`, `rejectEvent()`, `getUsers()`, `deleteUser()` |
| `UserService` | `getMe()`, `updateMe()`, `changePassword()`, `changeEmail()`, `deleteMe()`, `requestOrganizer()`, `getOrganizerRequest()` |

### `SeatHubService`

Manages the SignalR connection to `/hubs/seats` using `@microsoft/signalr`.

```ts
seatUpdates = signal<{ seatId: number; status: string }[]>([])
joinEvent(eventId: number): void
leaveEvent(eventId: number): void
```

`SeatMapComponent` calls `joinEvent()` on init and `leaveEvent()` on destroy. It reads `seatUpdates` to patch individual seat availability in real time without re-fetching the full seat list.

---

## Section 4: Key UI Flows

### Public Event Browse (`/events`)
Search input + filters (category dropdown, date range pickers, sort). Results in a responsive card grid using `EventCardComponent`. `PaginationComponent` at the bottom. No auth required.

### Event Detail + Seat Selection (`/events/:slug`)
Hero image, event metadata, ticket types with price/availability. "Book Now" checks `isAuthenticated()`; redirects to login with `returnUrl` if not. Seat map below — CSS grid grouped by section/row. Seat states:
- Available: `border-indigo-600 bg-white` (clickable)
- Selected: `bg-indigo-600 text-white`
- Reserved (other user): `bg-gray-200 cursor-not-allowed`
- Taken: `bg-gray-400 cursor-not-allowed`

Clicking an available seat calls `SeatService.reserve()` immediately to hold it. Selected seats tracked in a local `selectedSeats = signal<SeatReservationDto[]>([])`. "Proceed to Checkout" calls `BookingService.create()` then navigates to `/checkout/:bookingId`.

### Checkout (`/checkout/:bookingId`)
Two-column layout (order summary left, payment right). On mount: calls `PaymentService.initiate()` for `clientSecret`, loads `@stripe/stripe-js`, mounts `PaymentElement` into `#payment-element`. On submit: `stripe.confirmPayment()` (handles 3DS automatically). On success: `PaymentService.confirm(paymentIntentId)` then navigate to `/bookings/:id`. Stripe errors displayed via `AlertComponent`.

### My Bookings (`/bookings` + `/bookings/:id`)
Paginated list with status badges. Detail view shows QR code (`BookingQrComponent`), ticket items table, and cancel button for `Pending`/`Confirmed` bookings.

### Organizer Dashboard (`/organizer/events`)
Table of organizer's events with status, dates, and actions: Edit / Manage Tickets / View Bookings / Submit for Approval / Cancel. Ticket types managed on a sub-page per event. Bookings sub-page includes a QR scanner text input that calls `BookingService.validateQr()`.

### Admin Dashboard
- `/admin/events` — pending event cards with Approve / Reject (reason textarea) actions
- `/admin/organizer-requests` — table with Approve / Reject actions
- `/admin/users` — searchable, filterable table with Delete action
- `/admin/venues` — venue CRUD table; each row links to `/admin/venues/:id/seats` for bulk seat creation

---

## Section 5: Shared Components & Design System

### Tailwind Tokens

| Purpose | Classes |
|---|---|
| Page background | `bg-gray-50` |
| Card | `bg-white border border-gray-200 rounded-lg shadow-sm` |
| Primary button | `bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg px-4 py-2` |
| Secondary button | `border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg px-4 py-2` |
| Danger button | `bg-red-600 hover:bg-red-700 text-white rounded-lg px-4 py-2` |
| Heading | `text-gray-900 font-semibold` |
| Body text | `text-gray-600` |
| Muted | `text-gray-400` |
| Divider | `border-gray-200` |
| Focus ring | `focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2` |
| Status: success | `text-green-600 bg-green-50` |
| Status: warning | `text-amber-600 bg-amber-50` |
| Status: danger | `text-red-600 bg-red-50` |

### Shared Components

**`NavbarComponent`** — logo left, role-aware nav links center, user avatar/menu right. Hamburger on mobile via `menuOpen = signal(false)`. Unauthenticated: Login + Register links. User: My Bookings. Organizer: My Events + My Bookings. Admin: Admin dropdown.

**`EventCardComponent`** — inputs: `event: EventDto`. Renders image (`aspect-video object-cover`), title, date (via `IstDatePipe`), venue city, category badge. Ticket pricing is not shown on the card (not available in `EventDto`); it appears on the event detail page.

**`SeatMapComponent`** — inputs: `eventId`, `venueId`. Outputs: `seatSelected` (emits `ReserveSeatRequest`). Fetches available seats on init, groups by section/row, renders as scrollable CSS grid. Subscribes to `SeatHubService.seatUpdates` via `effect()` to patch seat states in real time.

**`StripePaymentComponent`** — input: `clientSecret: string`. Output: `paymentConfirmed` (emits `paymentIntentId: string`). Mounts Stripe `PaymentElement` on init. Handles submit and emits on success.

**`BookingQrComponent`** — input: `qrCode: string` (base64 PNG). Renders `<img>` with a download anchor.

**`PaginationComponent`** — inputs: `currentPage`, `totalPages`. Output: `pageChange`. Renders prev/next + page numbers.

**`LoadingSpinnerComponent`** — centered indigo spinner, used while async operations are in flight.

**`AlertComponent`** — inputs: `type: 'success' | 'error' | 'info'`, `message: string`. Dismissible via a close button.

### Pipes

**`IstDatePipe`** — transforms a UTC `Date` or ISO string to IST using `Intl.DateTimeFormat` with `timeZone: 'Asia/Kolkata'`.

**`CurrencyInrPipe`** — formats a `number` as `₹1,200.00`.

---

## Section 6: Error Handling & Testing

### Error Handling

- All services catch `HttpErrorResponse` and extract `error.message`, re-throwing as a plain `string`.
- Components hold `error = signal<string | null>(null)`; template shows `AlertComponent` when non-null.
- `AuthErrorInterceptor` intercepts 401 silently (refresh → retry); error never surfaces to the component unless refresh also fails.
- Stripe payment errors (declined card, 3DS failure) come from `stripe.confirmPayment()` result and are shown inline via `AlertComponent`.
- Unknown routes fall through to `NotFoundComponent` at `/404`.

### Testing

- **Framework:** Jasmine + Karma (Angular default)
- **Services:** `HttpClientTestingModule` to mock HTTP; assert signal state after method calls
- **Components:** `TestBed` shallow rendering; assert template output for loading / error / empty / populated states
- **Guards:** tested as plain functions with mocked `AuthService` signals
- **Pipes:** pure unit tests (input → expected output)
- Test files co-located with source (`auth.service.spec.ts` beside `auth.service.ts`)
- No E2E tests in scope

---

## Configuration

- API base URL: `http://localhost:5222` (from `launchSettings.json`; set in `environments/environment.ts`)
- Stripe publishable key: set in `environments/environment.ts` as `stripePublishableKey`
- SignalR hub: `http://localhost:5222/hubs/seats`
- Auth tokens: stored in `localStorage` under keys `ems_access_token` and `ems_refresh_token`

---

## Out of Scope

- Server-side rendering (SSR / Angular Universal)
- Push notifications
- Internationalisation (i18n)
- E2E tests
- CI/CD pipeline
