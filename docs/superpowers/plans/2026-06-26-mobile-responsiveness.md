# Mobile Responsiveness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every page of the EMSAngular frontend fully mobile-responsive — fixing navigation, tables, tap targets, and cramped layouts on small screens.

**Architecture:** Pure Tailwind utility + template changes plus one Angular signal for the admin sidebar drawer. No new services, no structural refactors. Desktop (`lg+`) layouts are unchanged throughout.

**Tech Stack:** Angular (standalone components, signals, OnPush), Tailwind CSS, custom design tokens in `tailwind.config.js`.

## Global Constraints

- **Git commit messages: 5 words or fewer.** No body, no bullet points, no co-author lines. (From CLAUDE.md.)
- All commands run from the repo root: `/Users/srimanikandanr/My Files/Presidio/Capstone Project`.
- Angular dir: `EMSAngular/`. Build with `cd EMSAngular && ng build`.
- Use `inject()` not constructor injection; `protected readonly` for template-only members; signals for state (per CLAUDE.md Angular conventions).
- Do NOT change desktop (`lg:`) behaviour — mobile-first additions only.
- Tailwind breakpoints: `sm:` = 640px, `lg:` = 1024px.

---

### Task 1: Reusable table scroll-hint utility + pagination tap targets

**Files:**
- Modify: `EMSAngular/src/styles.css` (add `.table-scroll-wrap` in the `@layer components` block, after `.data-table` rules ~line 119)
- Modify: `EMSAngular/src/app/shared/components/pagination/pagination.component.html`

**Interfaces:**
- Produces: a `.table-scroll-wrap` CSS class (horizontal scroll container with right-edge fade shadow), consumed by Task 3.

- [ ] **Step 1: Add the `table-scroll-wrap` utility to styles.css**

In `EMSAngular/src/styles.css`, inside the existing `@layer components { ... }` block, immediately after the `.data-table tbody tr + tr { ... }` rule (around line 119), add:

```css
  /* Horizontal-scroll wrapper for wide tables on mobile, with a right-edge
     fade that appears only while more content is scrollable. */
  .table-scroll-wrap {
    @apply overflow-x-auto;
    background:
      linear-gradient(to right, theme(colors.surface) 30%, rgba(255, 255, 255, 0)) left center,
      linear-gradient(to left, theme(colors.surface) 30%, rgba(255, 255, 255, 0)) right center,
      radial-gradient(farthest-side at 0 50%, rgba(36, 27, 46, 0.12), rgba(36, 27, 46, 0)) left center,
      radial-gradient(farthest-side at 100% 50%, rgba(36, 27, 46, 0.12), rgba(36, 27, 46, 0)) right center;
    background-repeat: no-repeat;
    background-size: 40px 100%, 40px 100%, 14px 100%, 14px 100%;
    background-attachment: local, local, scroll, scroll;
  }
```

- [ ] **Step 2: Bump pagination button tap targets**

In `EMSAngular/src/app/shared/components/pagination/pagination.component.html`:

Change line 1 from:
```html
    <nav class="flex items-center justify-center gap-1.5 py-8" *ngIf="totalPages > 1" aria-label="Pagination">
```
to:
```html
    <nav class="flex touch-manipulation items-center justify-center gap-2 py-8" *ngIf="totalPages > 1" aria-label="Pagination">
```

Change the page-number button (lines 4-5) from:
```html
      <button *ngFor="let p of pages()"
              class="h-9 w-9 rounded-full border text-sm font-medium transition"
```
to:
```html
      <button *ngFor="let p of pages()"
              class="h-10 w-10 rounded-full border text-sm font-medium transition"
```

- [ ] **Step 3: Verify the build compiles**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no errors (warnings about bundle size are fine).

- [ ] **Step 4: Commit**

```bash
git add EMSAngular/src/styles.css EMSAngular/src/app/shared/components/pagination/pagination.component.html
git commit -m "add table scroll hint utility"
```

---

### Task 2: Admin sidebar mobile drawer

**Files:**
- Modify: `EMSAngular/src/app/features/admin/admin-layout.component.ts`
- Modify: `EMSAngular/src/app/features/admin/admin-layout.component.html`
- Modify: `EMSAngular/src/app/features/admin/admin-layout.component.css`

**Interfaces:**
- Consumes: nothing from prior tasks.
- Produces: a self-contained drawer; no exports relied on by later tasks.

- [ ] **Step 1: Add drawer state + toggle to the component**

Replace the entire body of `EMSAngular/src/app/features/admin/admin-layout.component.ts` with:

```ts
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'ems-admin-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.css',
})
export class AdminLayoutComponent {
  protected readonly drawerOpen = signal(false);

  protected readonly links = [
    { path: 'events', label: 'Event approvals' },
    { path: 'organizer-requests', label: 'Organizer requests' },
    { path: 'users', label: 'Users' },
    { path: 'venues', label: 'Venues' },
  ];

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }
}
```

- [ ] **Step 2: Replace the layout template with the drawer markup**

Replace the entire contents of `EMSAngular/src/app/features/admin/admin-layout.component.html` with:

```html
    <div class="flex flex-col gap-8 lg:flex-row">
      <!-- Mobile drawer toggle (hidden on lg+) -->
      <button type="button" (click)="toggleDrawer()"
              class="btn-ghost self-start lg:hidden"
              aria-label="Open admin menu" [attr.aria-expanded]="drawerOpen()">
        <span class="font-mono text-xs uppercase tracking-eyebrow">☰ Admin menu</span>
      </button>

      <!-- Backdrop (mobile only, when open) -->
      @if (drawerOpen()) {
        <div class="fixed inset-0 z-30 bg-ink/40 lg:hidden" (click)="closeDrawer()" aria-hidden="true"></div>
      }

      <!-- Sidebar: static column on lg+, slide-in drawer on mobile -->
      <aside
        class="fixed inset-y-0 left-0 z-40 w-64 transform bg-surface p-5 shadow-lift transition-transform
               lg:static lg:z-auto lg:w-56 lg:shrink-0 lg:transform-none lg:bg-transparent lg:p-0 lg:shadow-none"
        [class.translate-x-0]="drawerOpen()"
        [class.-translate-x-full]="!drawerOpen()"
        [class.lg:translate-x-0]="true"
      >
        <p class="eyebrow mb-3 px-3">Admin</p>
        <nav class="flex flex-col gap-1">
          @for (item of links; track item.path) {
            <a
              [routerLink]="item.path"
              routerLinkActive="bg-surface text-ink lg:bg-surface"
              (click)="closeDrawer()"
              class="side-link"
            >{{ item.label }}</a>
          }
        </nav>
      </aside>

      <section class="min-w-0 flex-1">
        <router-outlet />
      </section>
    </div>
```

- [ ] **Step 3: Verify the build compiles**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no errors.

- [ ] **Step 4: Manually verify drawer behaviour**

Run: `cd EMSAngular && ng serve`
In the browser dev tools, switch to a mobile viewport (e.g. 375px wide) and navigate to `/admin`. Expected:
- "☰ Admin menu" button is visible; sidebar is off-screen.
- Clicking it slides the sidebar in from the left with a dark backdrop.
- Clicking a link or the backdrop closes the drawer.
- At desktop width (≥1024px) the toggle is hidden and the sidebar is pinned left as before.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/features/admin/admin-layout.component.ts EMSAngular/src/app/features/admin/admin-layout.component.html EMSAngular/src/app/features/admin/admin-layout.component.css
git commit -m "add admin sidebar mobile drawer"
```

---

### Task 3: Data tables — scroll wrapper + responsive headers

**Files:**
- Modify: `EMSAngular/src/app/features/admin/users/admin-users.component.html`
- Modify: `EMSAngular/src/app/features/admin/organizer-requests/organizer-requests.component.html`
- Modify: `EMSAngular/src/app/features/organizer/event-bookings/event-bookings.component.html`
- Modify: `EMSAngular/src/app/features/organizer/event-list/organizer-event-list.component.html`

**Interfaces:**
- Consumes: `.table-scroll-wrap` class from Task 1.

- [ ] **Step 1: admin-users — swap scroll wrapper + wrap the search form**

In `EMSAngular/src/app/features/admin/users/admin-users.component.html`:

Change line 4 from:
```html
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-5 flex gap-3">
```
to:
```html
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-5 flex flex-col gap-3 sm:flex-row">
```

Change line 10 from:
```html
    <div *ngIf="!loading()" class="overflow-x-auto">
```
to:
```html
    <div *ngIf="!loading()" class="table-scroll-wrap">
```

- [ ] **Step 2: organizer-requests — swap scroll wrapper**

In `EMSAngular/src/app/features/admin/organizer-requests/organizer-requests.component.html`, change line 6 from:
```html
    <div *ngIf="!loading()" class="overflow-x-auto">
```
to:
```html
    <div *ngIf="!loading()" class="table-scroll-wrap">
```

- [ ] **Step 3: event-bookings — swap scroll wrapper**

In `EMSAngular/src/app/features/organizer/event-bookings/event-bookings.component.html`, change line 7 from:
```html
    <div *ngIf="!loading()" class="overflow-x-auto">
```
to:
```html
    <div *ngIf="!loading()" class="table-scroll-wrap">
```

- [ ] **Step 4: organizer-event-list — swap scroll wrapper + wrap header row**

In `EMSAngular/src/app/features/organizer/event-list/organizer-event-list.component.html`:

Change line 1 from:
```html
    <div class="mb-6 flex items-end justify-between gap-4">
```
to:
```html
    <div class="mb-6 flex flex-wrap items-end justify-between gap-4">
```

Change line 11 from:
```html
    <div *ngIf="!loading()" class="overflow-x-auto">
```
to:
```html
    <div *ngIf="!loading()" class="table-scroll-wrap">
```

- [ ] **Step 5: Verify the build compiles**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/features/admin/users/admin-users.component.html EMSAngular/src/app/features/admin/organizer-requests/organizer-requests.component.html EMSAngular/src/app/features/organizer/event-bookings/event-bookings.component.html EMSAngular/src/app/features/organizer/event-list/organizer-event-list.component.html
git commit -m "make data tables mobile friendly"
```

---

### Task 4: Seat map + seat builder tap targets

**Files:**
- Modify: `EMSAngular/src/app/shared/components/seat-map/seat-map.component.html`
- Modify: `EMSAngular/src/app/features/admin/seats/admin-seats.component.html`

**Interfaces:** none shared.

- [ ] **Step 1: seat-map — bump seat button size**

In `EMSAngular/src/app/shared/components/seat-map/seat-map.component.html`, change the seat button class (line 17) from:
```html
                      class="h-8 w-8 rounded-md border text-xs font-medium transition"
```
to:
```html
                      class="h-10 w-10 touch-manipulation rounded-md border text-xs font-medium transition"
```

- [ ] **Step 2: admin-seats — bump seat builder button size**

In `EMSAngular/src/app/features/admin/seats/admin-seats.component.html`, change the seat cell button class (line 59) from:
```html
                      class="h-8 w-8 rounded-md border text-xs font-medium transition"
```
to:
```html
                      class="h-10 w-10 touch-manipulation rounded-md border text-xs font-medium transition"
```

- [ ] **Step 3: admin-seats — stack the Rows/Seats generate controls on mobile**

In `EMSAngular/src/app/features/admin/seats/admin-seats.component.html`, change the control row (lines 25-35) from:
```html
        <div class="card flex flex-wrap items-end gap-3 p-4">
          <label class="block space-y-1">
            <span class="field-label">Rows</span>
            <input type="number" [(ngModel)]="rows" min="1" class="field w-24" />
          </label>
          <label class="block space-y-1">
            <span class="field-label">Seats / row</span>
            <input type="number" [(ngModel)]="perRow" min="1" class="field w-24" />
          </label>
          <button type="button" (click)="generate()" class="btn-ghost">Generate grid</button>
        </div>
```
to:
```html
        <div class="card flex flex-col items-stretch gap-3 p-4 sm:flex-row sm:flex-wrap sm:items-end">
          <label class="block space-y-1">
            <span class="field-label">Rows</span>
            <input type="number" [(ngModel)]="rows" min="1" class="field w-full sm:w-24" />
          </label>
          <label class="block space-y-1">
            <span class="field-label">Seats / row</span>
            <input type="number" [(ngModel)]="perRow" min="1" class="field w-full sm:w-24" />
          </label>
          <button type="button" (click)="generate()" class="btn-ghost w-full sm:w-auto">Generate grid</button>
        </div>
```

- [ ] **Step 4: admin-seats — widen the paint palette gap for easier tapping**

In `EMSAngular/src/app/features/admin/seats/admin-seats.component.html`, change line 38 from:
```html
          <div class="mb-3 flex flex-wrap items-center gap-2">
```
to:
```html
          <div class="mb-3 flex flex-wrap items-center gap-3">
```

- [ ] **Step 5: Verify the build compiles**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/shared/components/seat-map/seat-map.component.html EMSAngular/src/app/features/admin/seats/admin-seats.component.html
git commit -m "enlarge seat tap targets"
```

---

### Task 5: Cramped list rows + inline forms

**Files:**
- Modify: `EMSAngular/src/app/features/admin/venues/admin-venues.component.html`
- Modify: `EMSAngular/src/app/features/organizer/ticket-types/ticket-types.component.html`
- Modify: `EMSAngular/src/app/features/admin/event-approvals/event-approvals.component.html`

**Interfaces:** none shared.

- [ ] **Step 1: admin-venues — stack each row on mobile**

In `EMSAngular/src/app/features/admin/venues/admin-venues.component.html`, change the list item (line 11) from:
```html
      <li *ngFor="let v of pagedVenues()" class="flex items-center justify-between gap-4 rounded-xl border border-line bg-surface p-4">
```
to:
```html
      <li *ngFor="let v of pagedVenues()" class="flex flex-col gap-3 rounded-xl border border-line bg-surface p-4 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
```

- [ ] **Step 2: ticket-types — stack each row on mobile**

In `EMSAngular/src/app/features/organizer/ticket-types/ticket-types.component.html`, change the list item (line 6) from:
```html
      <li *ngFor="let t of ticketTypes()" class="flex items-center justify-between gap-4 rounded-xl border border-line bg-surface p-4">
```
to:
```html
      <li *ngFor="let t of ticketTypes()" class="flex flex-col gap-2 rounded-xl border border-line bg-surface p-4 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
```

Change the Delete button (line 9) from:
```html
        <button (click)="remove(t.id)" class="link-danger">Delete</button>
```
to:
```html
        <button (click)="remove(t.id)" class="link-danger self-start sm:self-auto">Delete</button>
```

- [ ] **Step 3: event-approvals — stack the inline approve/reject form on mobile**

In `EMSAngular/src/app/features/admin/event-approvals/event-approvals.component.html`, change the action row (lines 11-16) from:
```html
        <div class="mt-4 flex flex-wrap items-center gap-2">
          <button (click)="approve(ev.id)" class="btn-primary btn-sm">Approve</button>
          <input [ngModel]="reasons()[ev.id] ?? ''" (ngModelChange)="setReason(ev.id, $event)"
                 aria-label="Rejection reason" placeholder="Rejection reason" class="field flex-1 py-1.5" />
          <button (click)="reject(ev.id)" class="btn-danger btn-sm">Reject</button>
        </div>
```
to:
```html
        <div class="mt-4 flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
          <input [ngModel]="reasons()[ev.id] ?? ''" (ngModelChange)="setReason(ev.id, $event)"
                 aria-label="Rejection reason" placeholder="Rejection reason" class="field w-full py-1.5 sm:flex-1" />
          <div class="flex gap-2">
            <button (click)="approve(ev.id)" class="btn-primary btn-sm w-full sm:w-auto">Approve</button>
            <button (click)="reject(ev.id)" class="btn-danger btn-sm w-full sm:w-auto">Reject</button>
          </div>
        </div>
```

- [ ] **Step 4: Verify the build compiles**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/features/admin/venues/admin-venues.component.html EMSAngular/src/app/features/organizer/ticket-types/ticket-types.component.html EMSAngular/src/app/features/admin/event-approvals/event-approvals.component.html
git commit -m "stack list rows on mobile"
```

---

### Task 6: Audit pass — detail/checkout/profile fine-tuning

**Files:**
- Modify: `EMSAngular/src/app/features/bookings/booking-detail/booking-detail.component.html`
- Modify: `EMSAngular/src/app/features/events/event-detail/event-detail.component.html`
- Modify: `EMSAngular/src/app/features/events/checkout/checkout.component.html`
- Modify: `EMSAngular/src/app/features/profile/profile.component.html`

**Interfaces:** none shared.

- [ ] **Step 1: booking-detail — responsive padding + gap**

In `EMSAngular/src/app/features/bookings/booking-detail/booking-detail.component.html`:

Change the booking item row (line 10) from:
```html
          <li *ngFor="let item of b.items" class="flex justify-between gap-4">
```
to:
```html
          <li *ngFor="let item of b.items" class="flex justify-between gap-2 sm:gap-4">
```

Change the QR card (line 22) from:
```html
      <section class="card flex items-center justify-center p-6">
```
to:
```html
      <section class="card flex items-center justify-center p-4 sm:p-6">
```

- [ ] **Step 2: event-detail — sticky footer mobile padding + safe area + selection gap**

In `EMSAngular/src/app/features/events/event-detail/event-detail.component.html`:

Change the selection list item (line 75) from:
```html
            <li *ngFor="let s of selected()" class="flex items-center justify-between gap-4">
```
to:
```html
            <li *ngFor="let s of selected()" class="flex items-center justify-between gap-2 sm:gap-4">
```

Change the sticky-bar inner container (line 86) from:
```html
        <div class="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3.5">
```
to:
```html
        <div class="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3.5 pb-[max(0.875rem,env(safe-area-inset-bottom))] sm:px-6">
```

- [ ] **Step 3: checkout — gap + truncate to stop overflow on tiny screens**

In `EMSAngular/src/app/features/events/checkout/checkout.component.html`, change the order summary item (lines 13-16) from:
```html
            <li *ngFor="let item of b.items" class="flex justify-between gap-4">
              <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
              <span class="font-mono">{{ item.unitPrice | inr }}</span>
            </li>
```
to:
```html
            <li *ngFor="let item of b.items" class="flex justify-between gap-2 sm:gap-4">
              <span class="min-w-0 truncate">{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
              <span class="shrink-0 font-mono">{{ item.unitPrice | inr }}</span>
            </li>
```

- [ ] **Step 4: profile — full-width danger button on mobile**

In `EMSAngular/src/app/features/profile/profile.component.html`, change the close-account button (line 151) from:
```html
          <button type="submit" [disabled]="closing()" class="btn-danger">Close account</button>
```
to:
```html
          <button type="submit" [disabled]="closing()" class="btn-danger w-full sm:w-auto">Close account</button>
```

- [ ] **Step 5: Verify the build compiles**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/features/bookings/booking-detail/booking-detail.component.html EMSAngular/src/app/features/events/event-detail/event-detail.component.html EMSAngular/src/app/features/events/checkout/checkout.component.html EMSAngular/src/app/features/profile/profile.component.html
git commit -m "polish detail and checkout mobile"
```

---

## Self-Review Notes

- **Spec coverage:** Section 1 → Task 2. Section 2 → Tasks 1 & 3. Section 3 → Tasks 1 & 4. Section 4 → Task 5. Section 5 → Task 6. All sections covered.
- **Organizer sidebar:** Spec confirms organizer area has no layout/sidebar — no task needed, correct.
- **Scanner / auth / home / event-list / booking-list / login:** Spec marks these already responsive — no changes, correct.
- **Type/class consistency:** `.table-scroll-wrap` defined in Task 1, consumed in Task 3. `drawerOpen`/`toggleDrawer`/`closeDrawer` defined and used together in Task 2.
- **Verification model:** Responsive Tailwind changes are not unit-testable; each task verifies via `ng build` plus (for the drawer) a manual viewport check, which is the appropriate test cycle here.
