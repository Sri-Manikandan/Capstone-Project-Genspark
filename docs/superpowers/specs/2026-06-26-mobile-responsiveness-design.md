# Mobile Responsiveness — Full Audit & Fix

**Date:** 2026-06-26  
**Approach:** Option B — full audit pass + targeted fixes  
**Scope:** All pages in `EMSAngular/src/app/features/` and relevant shared components

---

## Context

The Angular frontend uses Tailwind CSS with a custom design system (plum/teal/gold palette, Fraunces + Hanken Grotesk fonts). Most pages are already responsive. This spec covers the gaps identified in a full audit.

---

## Section 1 — Navigation & Sidebars

### Admin sidebar
- **Problem:** On `< lg` screens, the sidebar becomes a horizontal-scrolling strip — confusing UX.
- **Fix:** Add a hamburger button (top-left, visible on `< lg` only) that toggles a slide-in drawer overlay with a backdrop. The sidebar nav links render inside this drawer on mobile, and stay pinned left on `lg+` (no desktop change).
- **State:** `drawerOpen: boolean` signal in `AdminLayoutComponent`. Drawer closes on nav link click and backdrop click.
- **Files:** `admin-layout.component.html`, `admin-layout.component.css`

### Organizer area
- No layout wrapper or sidebar — organizer pages are standalone components routed directly. No sidebar fix needed here.

---

## Section 2 — Data Tables

### Scroll hint
- **Problem:** Tables wrapped in `overflow-x-auto` work but give no visual cue that content scrolls.
- **Fix:** Add a `table-scroll-wrap` utility class to `styles.css` using a CSS gradient technique to show a fade shadow on the right edge when content overflows.
- Replace bare `overflow-x-auto` divs on all 4 table pages with `<div class="table-scroll-wrap">`.
- **Files:** `styles.css`, `admin-users.component.html`, `admin-organizer-requests.component.html`, `organizer-event-bookings.component.html`, `organizer-event-list.component.html`

### Table header rows
- Title + action button rows using `flex items-end justify-between gap-4` get `flex-wrap` added so the button wraps under the title on narrow screens.

---

## Section 3 — Tap Targets & Interactive Elements

### Seat map buttons
- **Problem:** `h-8 w-8` (32px) — below 44px minimum tap target.
- **Fix:** Bump to `h-10 w-10` (40px). Keep `gap-1` between seats to avoid grid overflow. Scale label font to `text-xs`.
- **Files:** `seat-map.component.html`

### Admin seat builder buttons
- Same seat button fix (`h-10 w-10`).
- Control row (Rows + Seats/row inputs + Generate button): change from single-line `flex` with `w-24`/`w-28` to `flex-col sm:flex-row` with `w-full sm:w-24` on inputs.
- Paint palette row: bump `gap-2` to `gap-4` for larger touch targets between swatches.
- **Files:** `admin-seats.component.html`

### Pagination buttons
- **Problem:** `h-9 w-9` (36px) — marginally too small.
- **Fix:** Bump to `h-10 w-10`. Add `touch-manipulation` class to eliminate 300ms tap delay on mobile browsers.
- **Files:** `pagination.component.html`, `pagination.component.css`

---

## Section 4 — Cramped Flex Rows & List Items

### Admin venues list
- Each row: `flex items-center justify-between gap-4` with name, location/capacity, and 3 action links.
- **Fix:** `flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between`. Action links moved to a dedicated `flex gap-3` sub-row below venue info on mobile.
- **Files:** `admin-venues.component.html`

### Organizer ticket types list
- Same crowding issue.
- **Fix:** `flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between`. Delete button gets `w-full sm:w-auto`.
- **Files:** `ticket-types.component.html`

### Event approvals form
- Rejection reason input cramped between Approve/Reject buttons on one line.
- **Fix:** `flex flex-col gap-2 sm:flex-row`. Input gets `w-full`. Buttons get `w-full sm:w-auto`.
- **Files:** `event-approvals.component.html`

---

## Section 5 — Full Audit Pass

### Booking detail
- QR/stub card padding: `p-4 sm:p-6`.
- Booking item rows: `gap-2 sm:gap-4`.
- **Files:** `booking-detail.component.html`

### Event detail sticky footer
- Add `px-4 sm:px-6` explicit mobile padding to the fixed bar.
- Add `pb-[env(safe-area-inset-bottom)]` for iPhone notch support.
- **Files:** `event-detail.component.html`, `event-detail.component.css`

### Checkout
- Order summary item rows: `gap-2 sm:gap-4`.
- Seat label left side: `min-w-0 truncate` to prevent overflow on tiny screens.
- **Files:** `checkout.component.html`

### Profile
- Close-account danger button: `w-full sm:w-auto`.
- **Files:** `profile.component.html`

### Scanner, auth pages, home, event-list, booking-list, login
- Already fully responsive. No changes needed.

---

## What is NOT changing

- Desktop layouts — all `lg:` and above behaviour is unchanged.
- Component architecture — no new services, no structural refactors.
- Table structure — tables stay as tables, just scrollable with a hint shadow.
- Design tokens, colour palette, typography — untouched.

---

## File Change Summary

| File | Change type |
|---|---|
| `styles.css` | Add `table-scroll-wrap` utility |
| `admin-layout.component.html/css` | Drawer overlay for sidebar |
| `admin-users.component.html` | `table-scroll-wrap`, flex-wrap header |
| `admin-organizer-requests.component.html` | `table-scroll-wrap`, flex-wrap header |
| `admin-venues.component.html` | Stack list rows on mobile |
| `admin-seats.component.html` | Tap targets, stacking control row |
| `event-approvals.component.html` | Stack form row on mobile |
| `organizer-event-list.component.html` | `table-scroll-wrap`, flex-wrap header |
| `organizer-event-bookings.component.html` | `table-scroll-wrap`, flex-wrap header |
| `ticket-types.component.html` | Stack list rows on mobile |
| `seat-map.component.html` | Bump seat button tap targets |
| `pagination.component.html/css` | Bump button size, add touch-manipulation |
| `booking-detail.component.html` | Padding + gap adjustments |
| `event-detail.component.html/css` | Mobile padding + safe-area on sticky footer |
| `checkout.component.html` | Gap + truncate on seat labels |
| `profile.component.html` | Full-width button on mobile |
