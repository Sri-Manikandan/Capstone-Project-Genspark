# Venue Screens, Add-Venue Dialog & Interactive Seat-Map Builder — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a venue have multiple named screens, let each event run in one chosen screen, add venues via a modal dialog, and let admins define a screen's seats with an interactive grid builder.

**Architecture:** A "screen" is the existing `Seat.Section` value — no new table. `Event` gains a `Screen` string so availability is scoped to one screen. A new `PUT /api/v1/seat/screen` endpoint replaces a screen's seats wholesale (blocked when seats are in use). The admin seats page becomes an interactive grid builder; the venue page uses a reusable modal; the event form gains a screen dropdown.

**Tech Stack:** ASP.NET Core (EMSBLLLibrary/EMSDALLibrary/EMSModelLibrary), EF Core + PostgreSQL, AutoMapper; Angular 18 standalone components with signals; NUnit + Moq + FluentAssertions (backend), Jasmine/Karma (frontend).

## Global Constraints

- Git commit messages: **5 words or fewer**, no body, no co-author lines (CLAUDE.md global rule).
- BLL services inject **repository interfaces only**, never `EventContext`.
- All datetime fields map UTC→IST via `TimeHelper`; `Screen` is a plain string (no conversion).
- Angular: use `inject()`, `protected`/`readonly`, `ChangeDetectionStrategy.OnPush`, signals; selectors prefixed `ems-`; kebab-case filenames.
- Domain errors: throw `ValidationException`/`NotFoundException` from BLL; never return raw error strings.
- Backend build: `dotnet build EventManagementSystem/EMS.sln`. Backend tests: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj`.
- Frontend (run from `EMSAngular/`): `ng build`, `ng test`.

---

## Task 1: Add `Screen` to Event (model, DTOs, mapping, service, migration)

**Files:**
- Modify: `EventManagementSystem/EMSModelLibrary/Models/Event.cs`
- Modify: `EventManagementSystem/EMSModelLibrary/DTOs/EventDTOs.cs`
- Modify: `EventManagementSystem/EMSBLLLibrary/Services/EventService.cs`
- Test: `EventManagementSystem/EMSTests/Services/EventServiceTests.cs`
- Migration: `EventManagementSystem/EMSDALLibrary/Migrations/*_AddEventScreen.cs` (generated)

**Interfaces:**
- Produces: `Event.Screen : string`, `EventDto.Screen`, `CreateEventRequest.Screen`, `UpdateEventRequest.Screen` — all plain `string` defaulting to `""`.

- [ ] **Step 1: Write the failing test**

Add to `EventServiceTests.cs` (inside the fixture):

```csharp
[Test]
public async Task Create_WithScreen_PersistsScreen()
{
    _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Venue { Id = 1 });
    Event? saved = null;
    _eventRepo.Setup(r => r.Add(It.IsAny<Event>()))
        .Callback<Event>(e => saved = e)
        .ReturnsAsync((Event e) => e);

    var req = new CreateEventRequest
    {
        VenueId = 1, Title = "Show", Description = ValidDescription, Category = ValidCategory,
        ImageUrl = ValidImageUrl, StartTime = Start, EndTime = End, Screen = "Screen 2"
    };

    var result = await _sut.Create(10, req);

    saved!.Screen.Should().Be("Screen 2");
    result.Screen.Should().Be("Screen 2");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~EventServiceTests.Create_WithScreen_PersistsScreen"`
Expected: FAIL — `CreateEventRequest` / `EventDto` / `Event` have no `Screen` member (compile error).

- [ ] **Step 3: Add the `Screen` property to the model and DTOs**

In `Event.cs`, add after `RejectionReason`:

```csharp
public string Screen { get; set; } = string.Empty;
```

In `EventDTOs.cs`, add `public string Screen { get; set; } = string.Empty;` to **each** of `EventDto`, `CreateEventRequest`, and `UpdateEventRequest`.

(No `MappingProfile` change needed — AutoMapper maps the same-named `string` automatically.)

- [ ] **Step 4: Persist `Screen` in EventService**

In `EventService.Create`, add to the `new Event { ... }` initializer:

```csharp
Screen = request.Screen ?? string.Empty,
```

In `EventService.Update`, add before `ev.UpdatedAt = DateTime.UtcNow;`:

```csharp
ev.Screen = request.Screen ?? string.Empty;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~EventServiceTests.Create_WithScreen_PersistsScreen"`
Expected: PASS

- [ ] **Step 6: Create and verify the EF migration**

Run from repo root:

```bash
dotnet ef migrations add AddEventScreen --project EventManagementSystem/EMSDALLibrary --startup-project EventManagementSystem/EMSApplicationLayer
```

Expected: a new migration adding a `Screen` text column to `Events` with default `''`. Open the generated file and confirm `AddColumn<string>(name: "Screen", ... defaultValue: "")`. If the default is missing, set `defaultValue: ""` manually so existing rows get `""`.

- [ ] **Step 7: Build the solution**

Run: `dotnet build EventManagementSystem/EMS.sln`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "add event screen field"
```

---

## Task 2: Scope seat availability to the event's screen

**Files:**
- Modify: `EventManagementSystem/EMSDALLibrary/Repositories/SeatRepository.cs` (method `GetAvailableByEventId`)
- Test: `EventManagementSystem/EMSTests/Repositories/SeatRepositoryTests.cs` (create)

**Interfaces:**
- Consumes: `Event.Screen` (Task 1).
- Produces: unchanged signature `Task<List<Seat>> GetAvailableByEventId(int eventId)` — now returns only the event's screen when `Event.Screen` is non-empty.

- [ ] **Step 1: Write the failing test**

Create `EventManagementSystem/EMSTests/Repositories/SeatRepositoryTests.cs`:

```csharp
using EMSDALLibrary.Contexts;
using EMSDALLibrary.Repositories;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace EMSTests.Repositories
{
    [TestFixture]
    public class SeatRepositoryTests
    {
        private EventContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<EventContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new EventContext(options);
        }

        [Test]
        public async Task GetAvailableByEventId_FiltersBySection_WhenEventScreenSet()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1, Screen = "Screen 2" });
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "Screen 2", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);
            var result = await repo.GetAvailableByEventId(1);

            result.Should().ContainSingle().Which.Id.Should().Be(2);
        }

        [Test]
        public async Task GetAvailableByEventId_ReturnsWholeVenue_WhenScreenEmpty()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1, Screen = "" });
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "Screen 2", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);
            var result = await repo.GetAvailableByEventId(1);

            result.Should().HaveCount(2);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~SeatRepositoryTests.GetAvailableByEventId_FiltersBySection_WhenEventScreenSet"`
Expected: FAIL — currently returns both seats (count 2, not single).

- [ ] **Step 3: Add the screen filter**

In `SeatRepository.GetAvailableByEventId`, change the final query from:

```csharp
            return await _context.Seats
                .Where(s => s.VenueId == eventEntity.VenueId && !unavailableSeatIds.Contains(s.Id))
                .ToListAsync();
```

to:

```csharp
            return await _context.Seats
                .Where(s => s.VenueId == eventEntity.VenueId
                            && !unavailableSeatIds.Contains(s.Id)
                            && (eventEntity.Screen == "" || s.Section == eventEntity.Screen))
                .ToListAsync();
```

- [ ] **Step 4: Run both tests to verify they pass**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~SeatRepositoryTests"`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "scope seat availability to screen"
```

---

## Task 3: Save-screen endpoint (replace a screen's seats, blocked when in use)

**Files:**
- Modify: `EventManagementSystem/EMSModelLibrary/DTOs/SeatDTOs.cs`
- Modify: `EventManagementSystem/EMSDALLibrary/Interfaces/ISeatRepository.cs`
- Modify: `EventManagementSystem/EMSDALLibrary/Repositories/SeatRepository.cs`
- Modify: `EventManagementSystem/EMSBLLLibrary/Interfaces/ISeatService.cs`
- Modify: `EventManagementSystem/EMSBLLLibrary/Services/SeatService.cs`
- Modify: `EventManagementSystem/EMSApplicationLayer/Controllers/SeatController.cs`
- Test: `EventManagementSystem/EMSTests/Services/SeatServiceTests.cs`
- Test: `EventManagementSystem/EMSTests/Repositories/SeatRepositoryTests.cs`

**Interfaces:**
- Produces:
  - `ScreenSeatDto { string Row; int SeatNumber; string SeatType; }`
  - `SetScreenSeatsRequest { int VenueId; string Screen; List<ScreenSeatDto> Seats; }`
  - `ISeatRepository.ScreenHasActiveSeatUsage(int venueId, string section) : Task<bool>`
  - `ISeatRepository.ReplaceScreenSeats(int venueId, string section, List<Seat> seats) : Task`
  - `ISeatService.SetScreenSeats(SetScreenSeatsRequest) : Task<List<SeatDto>>`
  - `PUT /api/v1/seat/screen` (Admin)

- [ ] **Step 1: Add the DTOs**

In `SeatDTOs.cs`, append inside the namespace:

```csharp
    public class ScreenSeatDto
    {
        [Required]
        [StringLength(10, MinimumLength = 1)]
        public string Row { get; set; } = string.Empty;

        [Range(1, 10000)]
        public int SeatNumber { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string SeatType { get; set; } = string.Empty;
    }

    public class SetScreenSeatsRequest
    {
        [Range(1, int.MaxValue)]
        public int VenueId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Screen { get; set; } = string.Empty;

        public List<ScreenSeatDto> Seats { get; set; } = new();
    }
```

- [ ] **Step 2: Write the failing repository tests**

Add to `SeatRepositoryTests.cs`:

```csharp
        [Test]
        public async Task ReplaceScreenSeats_RemovesOldAndAddsNew()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 5, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            await repo.ReplaceScreenSeats(1, "Screen 1", new List<Seat>
            {
                new Seat { VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Premium" },
                new Seat { VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 2, SeatType = "Premium" },
            });

            var remaining = await repo.GetByVenueId(1);
            remaining.Should().HaveCount(2).And.OnlyContain(s => s.SeatType == "Premium");
        }

        [Test]
        public async Task ScreenHasActiveSeatUsage_True_WhenSeatBooked()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 7, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            ctx.Bookings.Add(new Booking { Id = 3, EventId = 1, BookingStatus = "Confirmed" });
            ctx.BookingItems.Add(new BookingItem { Id = 9, BookingId = 3, SeatId = 7 });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            (await repo.ScreenHasActiveSeatUsage(1, "Screen 1")).Should().BeTrue();
        }

        [Test]
        public async Task ScreenHasActiveSeatUsage_False_WhenNoUsage()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 8, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            (await repo.ScreenHasActiveSeatUsage(1, "Screen 1")).Should().BeFalse();
        }
```

> Note: confirm `Booking`/`BookingItem` property names by opening their models; `Booking.BookingStatus` and `BookingItem.{BookingId,SeatId}` are used by `GetAvailableByEventId` already, so they are correct.

- [ ] **Step 3: Run repo tests to verify they fail**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~SeatRepositoryTests.ReplaceScreenSeats_RemovesOldAndAddsNew"`
Expected: FAIL — `ReplaceScreenSeats` / `ScreenHasActiveSeatUsage` not defined (compile error).

- [ ] **Step 4: Add repository interface members**

In `ISeatRepository.cs`, add inside the interface:

```csharp
        Task<bool> ScreenHasActiveSeatUsage(int venueId, string section);
        Task ReplaceScreenSeats(int venueId, string section, List<Seat> seats);
```

- [ ] **Step 5: Implement them in SeatRepository**

In `SeatRepository.cs`, add these methods:

```csharp
        public async Task<bool> ScreenHasActiveSeatUsage(int venueId, string section)
        {
            var seatIds = await _context.Seats
                .Where(s => s.VenueId == venueId && s.Section == section)
                .Select(s => s.Id)
                .ToListAsync();
            if (seatIds.Count == 0) return false;

            var booked = await _context.BookingItems
                .Join(_context.Bookings, bi => bi.BookingId, b => b.Id, (bi, b) => new { bi.SeatId, b.BookingStatus })
                .AnyAsync(x => seatIds.Contains(x.SeatId) && x.BookingStatus != "Cancelled");
            if (booked) return true;

            var now = DateTime.UtcNow;
            return await _context.SeatReservations
                .AnyAsync(sr => seatIds.Contains(sr.SeatId) && sr.Status == "Active" && sr.ReservedUntil > now);
        }

        public async Task ReplaceScreenSeats(int venueId, string section, List<Seat> seats)
        {
            var existing = await _context.Seats
                .Where(s => s.VenueId == venueId && s.Section == section)
                .ToListAsync();
            _context.Seats.RemoveRange(existing);
            await _context.Seats.AddRangeAsync(seats);
            await _context.SaveChangesAsync();
        }
```

- [ ] **Step 6: Run repo tests to verify they pass**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~SeatRepositoryTests"`
Expected: PASS (5 tests total in this file)

- [ ] **Step 7: Write the failing service tests**

Add to `SeatServiceTests.cs`:

```csharp
        [Test]
        public async Task SetScreenSeats_ReplacesSeats_WhenNoActiveUsage()
        {
            _seatRepo.Setup(r => r.ScreenHasActiveSeatUsage(1, "Screen 1")).ReturnsAsync(false);
            _seatRepo.Setup(r => r.ReplaceScreenSeats(1, "Screen 1", It.IsAny<List<Seat>>())).Returns(Task.CompletedTask);

            var req = new SetScreenSeatsRequest
            {
                VenueId = 1, Screen = "Screen 1",
                Seats = new() { new ScreenSeatDto { Row = "A", SeatNumber = 1, SeatType = "Normal" } }
            };

            var result = await _sut.SetScreenSeats(req);

            result.Should().HaveCount(1);
            _seatRepo.Verify(r => r.ReplaceScreenSeats(1, "Screen 1",
                It.Is<List<Seat>>(l => l.Count == 1 && l[0].Section == "Screen 1" && l[0].SeatType == "Normal")), Times.Once);
        }

        [Test]
        public async Task SetScreenSeats_Throws_WhenScreenInUse()
        {
            _seatRepo.Setup(r => r.ScreenHasActiveSeatUsage(1, "Screen 1")).ReturnsAsync(true);

            var req = new SetScreenSeatsRequest
            {
                VenueId = 1, Screen = "Screen 1",
                Seats = new() { new ScreenSeatDto { Row = "A", SeatNumber = 1, SeatType = "Normal" } }
            };

            var act = async () => await _sut.SetScreenSeats(req);

            await act.Should().ThrowAsync<ValidationException>().WithMessage("*bookings*");
            _seatRepo.Verify(r => r.ReplaceScreenSeats(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<List<Seat>>()), Times.Never);
        }
```

- [ ] **Step 8: Run service tests to verify they fail**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~SeatServiceTests.SetScreenSeats_ReplacesSeats_WhenNoActiveUsage"`
Expected: FAIL — `SetScreenSeats` not defined (compile error).

- [ ] **Step 9: Add service interface member and implementation**

In `ISeatService.cs`, add:

```csharp
        Task<List<SeatDto>> SetScreenSeats(SetScreenSeatsRequest request);
```

In `SeatService.cs`, add:

```csharp
        public async Task<List<SeatDto>> SetScreenSeats(SetScreenSeatsRequest request)
        {
            if (request.VenueId <= 0)
                throw new ValidationException("VenueId must be greater than zero.");
            if (string.IsNullOrWhiteSpace(request.Screen))
                throw new ValidationException("Screen is required.");
            if (request.Seats == null || request.Seats.Count == 0)
                throw new ValidationException("At least one seat is required.");

            if (await _seatRepo.ScreenHasActiveSeatUsage(request.VenueId, request.Screen))
                throw new ValidationException("Cannot edit a screen that already has bookings.");

            var seats = request.Seats.Select(s => new Seat
            {
                VenueId = request.VenueId,
                Section = request.Screen,
                Row = s.Row,
                SeatNumber = s.SeatNumber,
                SeatType = s.SeatType
            }).ToList();

            await _seatRepo.ReplaceScreenSeats(request.VenueId, request.Screen, seats);
            return _mapper.Map<List<SeatDto>>(seats);
        }
```

- [ ] **Step 10: Run service tests to verify they pass**

Run: `dotnet test EventManagementSystem/EMSTests/EMSTests.csproj --filter "FullyQualifiedName~SeatServiceTests.SetScreenSeats"`
Expected: PASS (2 tests)

- [ ] **Step 11: Add the controller endpoint**

In `SeatController.cs`, add after the `BulkCreate` action:

```csharp
        [HttpPut("screen")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetScreen([FromBody] SetScreenSeatsRequest request)
        {
            var seats = await _seatService.SetScreenSeats(request);
            return Ok(seats);
        }
```

- [ ] **Step 12: Build and run full backend test suite**

Run: `dotnet build EventManagementSystem/EMS.sln && dotnet test EventManagementSystem/EMSTests/EMSTests.csproj`
Expected: Build succeeded; all tests pass.

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "add save screen endpoint"
```

---

## Task 4: Reusable modal component

**Files:**
- Create: `EMSAngular/src/app/shared/components/modal/modal.component.ts`
- Test: `EMSAngular/src/app/shared/components/modal/modal.component.spec.ts`

**Interfaces:**
- Produces: `ModalComponent` (`ems-modal`) with `readonly title = input('')`, `readonly open = input(false)`, `readonly closed = output<void>()`, and a default `<ng-content>` slot. Emits `closed` on backdrop click, the × button, and `Escape`.

- [ ] **Step 1: Write the failing test**

Create `modal.component.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { ModalComponent } from './modal.component';

@Component({
  standalone: true,
  imports: [ModalComponent],
  template: `<ems-modal [open]="true" title="Test" (closed)="onClosed()"><p>Body</p></ems-modal>`,
})
class HostComponent {
  closedCount = 0;
  onClosed(): void { this.closedCount += 1; }
}

describe('ModalComponent', () => {
  it('renders projected content and emits closed on × click', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Body');

    el.querySelector<HTMLButtonElement>('[aria-label="Close dialog"]')!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.closedCount).toBe(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run (from `EMSAngular/`): `ng test --include="**/modal.component.spec.ts" --watch=false`
Expected: FAIL — cannot find module `./modal.component`.

- [ ] **Step 3: Create the modal component**

Create `modal.component.ts`:

```ts
import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-modal',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="open()" class="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div class="absolute inset-0 bg-ink/40 backdrop-blur-sm" (click)="close()"></div>
      <div class="relative z-10 w-full max-w-xl rounded-2xl border border-line bg-surface p-6 shadow-xl">
        <div class="mb-4 flex items-center justify-between">
          <h2 class="eyebrow">{{ title() }}</h2>
          <button type="button" class="text-muted transition hover:text-ink" aria-label="Close dialog" (click)="close()">×</button>
        </div>
        <ng-content></ng-content>
      </div>
    </div>
  `,
})
export class ModalComponent {
  readonly title = input('');
  readonly open = input(false);
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  protected close(): void {
    if (this.open()) this.closed.emit();
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `ng test --include="**/modal.component.spec.ts" --watch=false`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "add reusable modal component"
```

---

## Task 5: Venue add/edit dialog

**Files:**
- Modify: `EMSAngular/src/app/features/admin/venues/admin-venues.component.ts`

**Interfaces:**
- Consumes: `ModalComponent` (Task 4), existing `VenueService`, `VenueDto`.
- Produces: dialog-driven add/edit; `layoutConfig` removed from the form (sent as `'{}'`).

- [ ] **Step 1: Import the modal and add dialog state**

In `admin-venues.component.ts`, add to imports array `ModalComponent` and its import line:

```ts
import { ModalComponent } from '../../../shared/components/modal/modal.component';
```

Add a `dialogOpen` signal alongside the other signals:

```ts
protected dialogOpen = signal(false);
```

- [ ] **Step 2: Drop `layoutConfig` from the form**

Change the form group to:

```ts
protected form = this.fb.nonNullable.group({
  name: ['', [Validators.required, Validators.minLength(2)]],
  address: ['', [Validators.required, Validators.minLength(5)]],
  city: ['', [Validators.required, Validators.minLength(2)]],
  totalCapacity: [1, [Validators.min(1)]],
});
```

- [ ] **Step 3: Update `save`, `edit`, `cancelEdit` to drive the dialog**

Replace those three methods with:

```ts
protected openAdd(): void {
  this.editingId.set(null);
  this.form.reset({ totalCapacity: 1 });
  this.dialogOpen.set(true);
}

protected save(): void {
  if (this.form.invalid) { this.form.markAllAsTouched(); return; }
  const payload = { ...this.form.getRawValue(), layoutConfig: '{}' };
  const id = this.editingId();
  const req$ = id === null
    ? this.venueService.create(payload)
    : this.venueService.update(id, payload);
  req$.subscribe({
    next: () => { this.closeDialog(); this.load(); },
    error: (m: string) => this.error.set(m),
  });
}

protected edit(v: VenueDto): void {
  this.editingId.set(v.id);
  this.form.setValue({ name: v.name, address: v.address, city: v.city, totalCapacity: v.totalCapacity });
  this.dialogOpen.set(true);
}

protected closeDialog(): void {
  this.dialogOpen.set(false);
  this.editingId.set(null);
}
```

> `CreateVenueRequest` still requires `layoutConfig`; spreading `layoutConfig: '{}'` satisfies it. Keep `cancelEdit` removed — `closeDialog` replaces it.

- [ ] **Step 4: Update the template**

Replace the inline `<form>...</form>` block at the bottom of the template with an "Add venue" button (place it above the `<ul>`), plus the modal at the end:

```html
<div class="mb-6 flex justify-end">
  <button type="button" (click)="openAdd()" class="btn-primary">+ Add venue</button>
</div>
```

```html
<ems-modal [open]="dialogOpen()" [title]="editingId() ? 'Edit venue' : 'Add venue'" (closed)="closeDialog()">
  <form [formGroup]="form" (ngSubmit)="save()" class="grid grid-cols-1 gap-3 sm:grid-cols-2">
    <input formControlName="name" placeholder="Name" class="field" />
    <input formControlName="city" placeholder="City" class="field" />
    <input formControlName="address" placeholder="Address" class="field sm:col-span-2" />
    <input formControlName="totalCapacity" type="number" placeholder="Capacity" class="field sm:col-span-2" />
    <div class="flex gap-3 sm:col-span-2">
      <button type="submit" class="btn-primary">{{ editingId() ? 'Save changes' : 'Add venue' }}</button>
      <button type="button" (click)="closeDialog()" class="btn-ghost">Cancel</button>
    </div>
  </form>
</ems-modal>
```

- [ ] **Step 5: Build to verify it compiles**

Run (from `EMSAngular/`): `ng build`
Expected: Build succeeds. (Manually verify in `ng serve` that "+ Add venue" opens the dialog and Edit pre-fills it.)

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "venue add edit dialog"
```

---

## Task 6: Frontend seat models + service + grid helpers

**Files:**
- Modify: `EMSAngular/src/app/core/models/seat.model.ts`
- Modify: `EMSAngular/src/app/core/services/seat.service.ts`
- Create: `EMSAngular/src/app/features/admin/seats/seat-grid.ts`
- Test: `EMSAngular/src/app/features/admin/seats/seat-grid.spec.ts`

**Interfaces:**
- Produces:
  - `ScreenSeat { row: string; seatNumber: number; seatType: string; }`
  - `SetScreenSeatsRequest { venueId: number; screen: string; seats: ScreenSeat[]; }`
  - `SeatService.setScreenSeats(req): Observable<SeatDto[]>`
  - `BuilderCell { row: string; number: number; type: string; active: boolean; }`
  - `rowLabel(index: number): string`
  - `generateGrid(rows: number, perRow: number, defaultType: string): BuilderCell[][]`
  - `gridToSeats(grid: BuilderCell[][]): ScreenSeat[]`
  - `seatsToGrid(seats: SeatDto[]): BuilderCell[][]`

- [ ] **Step 1: Add model interfaces**

In `seat.model.ts`, append:

```ts
export interface ScreenSeat {
  row: string;
  seatNumber: number;
  seatType: string;
}

export interface SetScreenSeatsRequest {
  venueId: number;
  screen: string;
  seats: ScreenSeat[];
}
```

- [ ] **Step 2: Add the service method**

In `seat.service.ts`, add `SetScreenSeatsRequest` to the model import list, then add:

```ts
setScreenSeats(req: SetScreenSeatsRequest): Observable<SeatDto[]> {
  return this.http.put<SeatDto[]>(`${this.base}/screen`, req)
    .pipe(catchError(e => throwError(() => extractError(e))));
}
```

- [ ] **Step 3: Write the failing helper tests**

Create `seat-grid.spec.ts`:

```ts
import { rowLabel, generateGrid, gridToSeats, seatsToGrid } from './seat-grid';

describe('seat-grid helpers', () => {
  it('rowLabel produces A, Z, AA', () => {
    expect(rowLabel(0)).toBe('A');
    expect(rowLabel(25)).toBe('Z');
    expect(rowLabel(26)).toBe('AA');
  });

  it('generateGrid builds rows x perRow active cells', () => {
    const grid = generateGrid(2, 3, 'Normal');
    expect(grid.length).toBe(2);
    expect(grid[0].length).toBe(3);
    expect(grid[0][0]).toEqual({ row: 'A', number: 1, type: 'Normal', active: true });
    expect(grid[1][2].row).toBe('B');
  });

  it('gridToSeats skips inactive cells and renumbers per row', () => {
    const grid = generateGrid(1, 3, 'Normal');
    grid[0][1].active = false; // carve an aisle in the middle
    const seats = gridToSeats(grid);
    expect(seats).toEqual([
      { row: 'A', seatNumber: 1, seatType: 'Normal' },
      { row: 'A', seatNumber: 2, seatType: 'Normal' },
    ]);
  });

  it('seatsToGrid rebuilds a grid from saved seats', () => {
    const grid = seatsToGrid([
      { id: 1, venueId: 1, section: 'S1', row: 'A', seatNumber: 1, seatType: 'Premium' },
      { id: 2, venueId: 1, section: 'S1', row: 'A', seatNumber: 2, seatType: 'Premium' },
    ]);
    expect(grid.length).toBe(1);
    expect(grid[0].map(c => c.type)).toEqual(['Premium', 'Premium']);
  });
});
```

- [ ] **Step 4: Run tests to verify they fail**

Run (from `EMSAngular/`): `ng test --include="**/seat-grid.spec.ts" --watch=false`
Expected: FAIL — cannot find module `./seat-grid`.

- [ ] **Step 5: Implement the helpers**

Create `seat-grid.ts`:

```ts
import { SeatDto, ScreenSeat } from '../../../core/models/seat.model';

export interface BuilderCell {
  row: string;
  number: number;
  type: string;
  active: boolean;
}

export function rowLabel(index: number): string {
  let n = index + 1;
  let s = '';
  while (n > 0) {
    const rem = (n - 1) % 26;
    s = String.fromCharCode(65 + rem) + s;
    n = Math.floor((n - 1) / 26);
  }
  return s;
}

export function generateGrid(rows: number, perRow: number, defaultType: string): BuilderCell[][] {
  const grid: BuilderCell[][] = [];
  for (let r = 0; r < rows; r++) {
    const label = rowLabel(r);
    const cells: BuilderCell[] = [];
    for (let c = 0; c < perRow; c++) {
      cells.push({ row: label, number: c + 1, type: defaultType, active: true });
    }
    grid.push(cells);
  }
  return grid;
}

export function gridToSeats(grid: BuilderCell[][]): ScreenSeat[] {
  const seats: ScreenSeat[] = [];
  for (const row of grid) {
    let seatNo = 0;
    for (const cell of row) {
      if (!cell.active) continue;
      seatNo += 1;
      seats.push({ row: cell.row, seatNumber: seatNo, seatType: cell.type });
    }
  }
  return seats;
}

export function seatsToGrid(seats: SeatDto[]): BuilderCell[][] {
  const byRow = new Map<string, SeatDto[]>();
  for (const s of seats) {
    if (!byRow.has(s.row)) byRow.set(s.row, []);
    byRow.get(s.row)!.push(s);
  }
  return [...byRow.entries()]
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([row, rowSeats]) =>
      rowSeats
        .sort((a, b) => a.seatNumber - b.seatNumber)
        .map(s => ({ row, number: s.seatNumber, type: s.seatType, active: true })));
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `ng test --include="**/seat-grid.spec.ts" --watch=false`
Expected: PASS (4 tests)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "add seat grid helpers"
```

---

## Task 7: Interactive seat builder UI

**Files:**
- Modify: `EMSAngular/src/app/features/admin/seats/admin-seats.component.ts` (full rewrite)

**Interfaces:**
- Consumes: `seat-grid.ts` helpers (Task 6), `SeatService.getByVenue`/`setScreenSeats`, `SeatDto`.
- Produces: screen manager + grid builder at `/admin/venues/:id/seats`.

- [ ] **Step 1: Rewrite the component**

Replace the entire contents of `admin-seats.component.ts` with:

```ts
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SeatService } from '../../../core/services/seat.service';
import { SeatDto } from '../../../core/models/seat.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { BuilderCell, generateGrid, gridToSeats, seatsToGrid } from './seat-grid';

const AISLE = 'Aisle';

@Component({
  selector: 'ems-admin-seats',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Screens & seats</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-alert type="success" [message]="success()" (dismissed)="success.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="grid grid-cols-1 gap-6 lg:grid-cols-[14rem_1fr]">
      <!-- Screen list -->
      <aside class="space-y-2">
        <h2 class="eyebrow mb-2">Screens</h2>
        <button *ngFor="let s of screens()" type="button"
                class="block w-full rounded-lg border px-3 py-2 text-left text-sm transition"
                [class.border-plum]="s === selectedScreen()"
                [class.bg-paper]="s === selectedScreen()"
                [class.border-line]="s !== selectedScreen()"
                (click)="selectScreen(s)">{{ s }}</button>
        <div class="flex gap-2 pt-2">
          <input [(ngModel)]="newScreenName" placeholder="New screen" class="field flex-1" />
          <button type="button" (click)="addScreen()" class="btn-ghost">Add</button>
        </div>
      </aside>

      <!-- Builder -->
      <section *ngIf="selectedScreen() as screen" class="space-y-4">
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

        <div class="card p-4">
          <div class="mb-3 flex flex-wrap items-center gap-2">
            <span class="field-label">Paint:</span>
            <button *ngFor="let t of palette()" type="button"
                    class="rounded-lg border px-2.5 py-1 text-xs transition"
                    [class.border-plum]="t === paint()"
                    [class.bg-plum]="t === paint()"
                    [class.text-white]="t === paint()"
                    [class.border-line]="t !== paint()"
                    (click)="paint.set(t)">{{ t }}</button>
            <input [(ngModel)]="newType" placeholder="+ type" class="field w-28" />
            <button type="button" (click)="addType()" class="btn-ghost">Add type</button>
          </div>

          <div class="mb-4 rounded-xl bg-paper py-2 text-center font-mono text-[0.66rem] uppercase tracking-eyebrow text-muted">
            Screen — {{ screen }}
          </div>

          <div class="space-y-1.5 overflow-x-auto select-none" (mouseleave)="painting = false">
            <div *ngFor="let row of grid(); let r = index" class="flex items-center gap-1.5">
              <span class="w-6 font-mono text-xs text-muted">{{ row[0]?.row }}</span>
              <button *ngFor="let cell of row; let c = index" type="button"
                      class="h-8 w-8 rounded-md border text-xs font-medium transition"
                      [class.border-dashed]="!cell.active"
                      [class.border-line]="!cell.active"
                      [class.text-muted]="!cell.active"
                      [class.border-plum]="cell.active && cell.type !== 'Premium'"
                      [class.text-plum]="cell.active && cell.type !== 'Premium'"
                      [class.bg-surface]="cell.active && cell.type !== 'Premium'"
                      [class.bg-plum]="cell.active && cell.type === 'Premium'"
                      [class.text-white]="cell.active && cell.type === 'Premium'"
                      (mousedown)="startPaint(r, c)"
                      (mouseenter)="dragPaint(r, c)">{{ cell.active ? '' : '·' }}</button>
            </div>
          </div>

          <div class="mt-4 flex gap-3">
            <button type="button" (click)="save()" class="btn-primary">Save screen</button>
            <span class="self-center text-sm text-muted">{{ seatCount() }} seats</span>
          </div>
        </div>
      </section>
    </div>
  `,
})
export class AdminSeatsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private seatService = inject(SeatService);

  protected seats = signal<SeatDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected success = signal('');
  protected selectedScreen = signal<string | null>(null);
  protected grid = signal<BuilderCell[][]>([]);
  protected paint = signal('Normal');
  protected palette = signal<string[]>(['Normal', 'Premium', AISLE]);

  protected rows = 6;
  protected perRow = 10;
  protected newScreenName = '';
  protected newType = '';
  protected painting = false;

  private venueId = Number(this.route.snapshot.paramMap.get('id'));

  protected screens = computed(() => [...new Set(this.seats().map(s => s.section))].sort());
  protected seatCount = computed(() => gridToSeats(this.grid()).length);

  ngOnInit(): void { this.load(); }

  protected selectScreen(name: string): void {
    this.selectedScreen.set(name);
    const screenSeats = this.seats().filter(s => s.section === name);
    this.grid.set(seatsToGrid(screenSeats));
  }

  protected addScreen(): void {
    const name = this.newScreenName.trim();
    if (!name) return;
    this.newScreenName = '';
    this.selectedScreen.set(name);
    this.grid.set([]);
  }

  protected addType(): void {
    const t = this.newType.trim();
    if (!t || this.palette().includes(t)) { this.newType = ''; return; }
    this.palette.update(p => [...p.slice(0, -1), t, AISLE]); // keep Aisle last
    this.newType = '';
  }

  protected generate(): void {
    const base = this.paint() === AISLE ? 'Normal' : this.paint();
    this.grid.set(generateGrid(Math.max(1, this.rows), Math.max(1, this.perRow), base));
  }

  protected startPaint(r: number, c: number): void {
    this.painting = true;
    this.applyPaint(r, c);
  }

  protected dragPaint(r: number, c: number): void {
    if (this.painting) this.applyPaint(r, c);
  }

  private applyPaint(r: number, c: number): void {
    this.grid.update(g => {
      const next = g.map(row => row.map(cell => ({ ...cell })));
      const cell = next[r]?.[c];
      if (!cell) return g;
      if (this.paint() === AISLE) { cell.active = false; }
      else { cell.active = true; cell.type = this.paint(); }
      return next;
    });
  }

  protected save(): void {
    const screen = this.selectedScreen();
    if (!screen) return;
    const seats = gridToSeats(this.grid());
    if (seats.length === 0) { this.error.set('Add at least one seat before saving.'); return; }
    this.seatService.setScreenSeats({ venueId: this.venueId, screen, seats }).subscribe({
      next: () => { this.success.set('Screen saved.'); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.seatService.getByVenue(this.venueId).subscribe({
      next: s => {
        this.seats.set(s);
        this.loading.set(false);
        const current = this.selectedScreen();
        if (current && this.screens().includes(current)) this.selectScreen(current);
      },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

> Note: confirm `AlertComponent` supports `type="success"`; if it only supports `error`, drop the success `<ems-alert>` line and keep `success` unused, or reuse the error alert. Open `shared/components/alert/alert.component.ts` to check before finalizing.

- [ ] **Step 2: Build to verify it compiles**

Run (from `EMSAngular/`): `ng build`
Expected: Build succeeds.

- [ ] **Step 3: Manual smoke test**

Run `ng serve`, log in as admin (`admin@ems.com` / `Test@1234`), open a venue's Seats page. Verify: add a screen, set rows/seats, Generate, paint Premium, mark an Aisle, Save; reload shows the seats; an existing booked screen returns the "already has bookings" error on save.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "add interactive seat builder"
```

---

## Task 8: Event form screen dropdown

**Files:**
- Modify: `EMSAngular/src/app/core/models/event.model.ts`
- Modify: `EMSAngular/src/app/core/services/event.service.ts` (only if `create`/`update` strip fields — verify)
- Modify: `EMSAngular/src/app/features/organizer/event-form/event-form.component.ts`

**Interfaces:**
- Consumes: `SeatService.getByVenue` to derive a venue's screens; `EventDto.screen` (Task 1 backend).
- Produces: `screen` on `EventDto`/`CreateEventRequest`/`UpdateEventRequest`; a screen `<select>` in the event form.

- [ ] **Step 1: Add `screen` to the frontend event models**

In `event.model.ts`, add `screen: string;` to `EventDto`, and `screen: string;` to both `CreateEventRequest` and `UpdateEventRequest`.

- [ ] **Step 2: Wire the form**

In `event-form.component.ts`:

Add imports:

```ts
import { SeatService } from '../../../core/services/seat.service';
```

Add the injected service and a screens signal near the other fields:

```ts
private seatService = inject(SeatService);
protected screens = signal<string[]>([]);
```

Add `screen` to the form group:

```ts
screen: [''],
```

Add a method to load screens for a venue and call it on venue change + on edit load:

```ts
protected loadScreens(venueId: number): void {
  if (!venueId) { this.screens.set([]); return; }
  this.seatService.getByVenue(venueId).subscribe({
    next: seats => this.screens.set([...new Set(seats.map(s => s.section))].sort()),
    error: () => this.screens.set([]),
  });
}
```

In `ngOnInit`, after patching the form on edit, add `this.loadScreens(ev.venueId);` and patch `screen: ev.screen`. In `submit`, include `screen: v.screen` in both the create payload (`this.eventService.create(v)` already passes the whole value — ensure `screen` is in `v`) and the update payload object.

Concretely, change the update call payload to:

```ts
this.eventService.update(id, {
  title: v.title, description: v.description, startTime: v.startTime,
  endTime: v.endTime, imageUrl: v.imageUrl, category: v.category, screen: v.screen,
}).subscribe(done);
```

- [ ] **Step 3: Add the screen `<select>` to the template**

After the venue `<select>`, add:

```html
<select formControlName="screen" class="field">
  <option value="">Whole venue (all screens)</option>
  <option *ngFor="let s of screens()" [value]="s">{{ s }}</option>
</select>
```

And trigger `loadScreens` when the venue changes — add `(change)="loadScreens(form.controls.venueId.value)"` to the venue `<select>`.

- [ ] **Step 4: Build to verify it compiles**

Run (from `EMSAngular/`): `ng build`
Expected: Build succeeds. Manually verify creating an event lets you pick a screen, and the booking page for that event shows only that screen's seats.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "add screen to event form"
```

---

## Task 9: Show the screen name on the booking seat-map

**Files:**
- Modify: `EMSAngular/src/app/shared/components/seat-map/seat-map.component.ts`
- Modify: caller (event-detail) to pass the event's screen — verify in `EMSAngular/src/app/features/events/event-detail/event-detail.component.ts`

**Interfaces:**
- Consumes: `EventDto.screen`.
- Produces: optional `readonly screenName = input('')` on `SeatMapComponent`; header shows it when present.

- [ ] **Step 1: Add the input and use it in the header**

In `seat-map.component.ts`, add to the inputs block:

```ts
@Input() screenName = '';
```

Change the "Stage / Screen" header div text to:

```html
{{ screenName ? screenName : 'Stage / Screen' }}
```

- [ ] **Step 2: Pass it from the caller**

In `event-detail.component.ts`, find the `<ems-seat-map ...>` usage and add `[screenName]="event()?.screen ?? ''"` (adjust to the local event signal/variable name used there).

- [ ] **Step 3: Build to verify it compiles**

Run (from `EMSAngular/`): `ng build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "show screen name on seatmap"
```

---

## Final verification

- [ ] **Backend:** `dotnet build EventManagementSystem/EMS.sln && dotnet test EventManagementSystem/EMSTests/EMSTests.csproj` — all pass.
- [ ] **Frontend:** `cd EMSAngular && ng build && ng test --watch=false` — build clean, specs pass.
- [ ] **DB:** `dotnet ef database update --project EventManagementSystem/EMSDALLibrary --startup-project EventManagementSystem/EMSApplicationLayer` applied.
- [ ] **Manual flow:** admin adds venue via dialog → builds two screens with the grid builder → organizer creates an event in "Screen 2" → booking page shows only Screen 2 seats → re-saving a booked screen is rejected.
