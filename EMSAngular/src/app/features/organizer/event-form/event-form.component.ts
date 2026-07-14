import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { SeatService } from '../../../core/services/seat.service';
import { ToastService } from '../../../core/services/toast.service';
import { VenueDto } from '../../../core/models/venue.model';
import { CreateEventWithScreeningsRequest } from '../../../core/models/event.model';
import { SeatDto } from '../../../core/models/seat.model';
import { EVENT_CATEGORIES } from '../../../core/constants/event-categories';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { endAfterStart, httpUrl, minLeadTime, notBlank, selectRequired } from '../../../shared/validators/form-validators';
import { RouterLink } from '@angular/router';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

@Component({
  selector: 'ems-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FieldErrorComponent, RouterLink, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-form.component.html',
})
export class EventFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);
  private venueService = inject(VenueService);
  private seatService = inject(SeatService);
  private toast = inject(ToastService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // Signal so an edited event whose stored category predates this list still shows selected.
  protected categoryOptions = signal<string[]>([...EVENT_CATEGORIES]);
  protected venues = signal<VenueDto[]>([]);
  protected screens = signal<string[]>([]);
  // Distinct screens chosen across the showtime rows, and the seat types every one of them
  // offers — a shared ticket category may only use a seat type present on all selected screens.
  protected selectedScreens = signal<string[]>([]);
  protected seatTypes = signal<string[]>([]);
  // All of the venue's seats; the screen list, seat types, and per-screen capacities derive from these.
  private venueSeats = signal<SeatDto[]>([]);
  protected eventId = signal<number | null>(null);
  protected isEdit = computed(() => this.eventId() !== null);

  form = this.fb.nonNullable.group({
    venueId: [0, [selectRequired]],
    title: ['', [Validators.required, notBlank, Validators.minLength(2), Validators.maxLength(200)]],
    description: ['', [Validators.required, notBlank, Validators.minLength(1), Validators.maxLength(2000)]],
    imageUrl: ['', [Validators.required, httpUrl]],
    category: ['', [Validators.required]],
    // Edit-only single-screen fields; a created event carries its screens/times on `showtimes`.
    screen: [''],
    startTime: ['', [Validators.required, minLeadTime(48)]],
    endTime: ['', Validators.required],
    // Create-only: one screening per showtime row, plus ticket categories shared across them.
    showtimes: this.fb.array<ReturnType<EventFormComponent['showtimeGroup']>>([]),
    ticketTypes: this.fb.array<ReturnType<EventFormComponent['categoryGroup']>>([]),
  }, { validators: endAfterStart('startTime', 'endTime') });

  protected get showtimes(): FormArray<ReturnType<EventFormComponent['showtimeGroup']>> {
    return this.form.controls.showtimes;
  }

  protected get categories(): FormArray<ReturnType<EventFormComponent['categoryGroup']>> {
    return this.form.controls.ticketTypes;
  }

  ngOnInit(): void {
    this.venueService.list().subscribe({ next: v => this.venues.set(v), error: (m: string) => this.toast.error(m) });
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.eventId.set(Number(idParam));
      this.eventService.getById(Number(idParam)).subscribe({
        next: ev => {
          // Keep a legacy/out-of-list category selectable rather than silently blanking it.
          if (ev.category && !this.categoryOptions().includes(ev.category))
            this.categoryOptions.update(list => [ev.category, ...list]);
          this.form.patchValue({
            venueId: ev.venueId, title: ev.title, description: ev.description,
            startTime: ev.startTime.slice(0, 16), endTime: ev.endTime.slice(0, 16),
            imageUrl: ev.imageUrl, category: ev.category, screen: ev.screen,
          });
          this.onVenueChange(ev.venueId);
        },
        error: (m: string) => this.toast.error(m),
      });
    } else {
      // Create mode uses per-showtime times, so the single event-level time fields don't apply.
      this.form.controls.startTime.clearValidators();
      this.form.controls.endTime.clearValidators();
      this.form.controls.startTime.updateValueAndValidity();
      this.form.controls.endTime.updateValueAndValidity();
      this.addShowtime();
      this.addCategory();
    }
  }

  protected onVenueChange(venueId: number): void {
    if (!venueId) { this.venueSeats.set([]); this.screens.set([]); this.refreshScreenDerivedInfo(); return; }
    this.seatService.getByVenue(venueId).subscribe({
      next: seats => {
        this.venueSeats.set(seats);
        this.screens.set([...new Set(seats.map(s => s.section))].sort());
        // Screens differ per venue, so clear any showtime screen picks that no longer apply.
        if (!this.isEdit())
          for (const st of this.showtimes.controls) st.controls.screen.setValue('');
        this.refreshScreenDerivedInfo();
      },
      // Surface the failure instead of silently blanking the lists, which would otherwise
      // look identical to "this venue has no seats yet".
      error: (m: string) => { this.venueSeats.set([]); this.screens.set([]); this.refreshScreenDerivedInfo(); this.toast.error(m); },
    });
  }

  protected onShowtimeScreenChange(): void {
    this.refreshScreenDerivedInfo();
  }

  // A shared ticket category must be sellable on every chosen screen, so the offered seat types
  // are the intersection across the selected screens. Recompute whenever that set changes.
  private refreshScreenDerivedInfo(): void {
    const screens = [...new Set(this.showtimes.controls.map(c => c.controls.screen.value).filter(Boolean))];
    this.selectedScreens.set(screens);

    const seats = this.venueSeats();
    let types: string[] = [];
    if (screens.length) {
      const perScreen = screens.map(sc => new Set(seats.filter(s => s.section === sc).map(s => s.seatType)));
      types = [...perScreen[0]].filter(t => perScreen.every(set => set.has(t))).sort();
    }
    this.seatTypes.set(types);

    // Drop any category seat type no longer valid across all selected screens.
    const valid = new Set(types);
    for (const category of this.categories.controls) {
      if (category.controls.seatType.value && !valid.has(category.controls.seatType.value))
        category.controls.seatType.setValue('');
    }
  }

  // Quantity maps to seat-type capacity, so a seat type may back only one category.
  // Exclude types already chosen by other rows from this row's dropdown.
  protected availableSeatTypesFor(index: number): string[] {
    const taken = new Set(
      this.categories.controls
        .filter((_, i) => i !== index)
        .map(c => c.controls.seatType.value)
        .filter(Boolean));
    return this.seatTypes().filter(t => !taken.has(t));
  }

  // A shared category's quantity varies by screen, so show the per-screen counts.
  protected capacityBreakdown(seatType: string): { screen: string; count: number }[] {
    const seats = this.venueSeats();
    return this.selectedScreens().map(screen => ({
      screen,
      count: seats.filter(s => s.section === screen && s.seatType === seatType).length,
    }));
  }

  protected totalCapacityFor(seatType: string): number {
    return this.capacityBreakdown(seatType).reduce((sum, b) => sum + b.count, 0);
  }

  private showtimeGroup() {
    return this.fb.nonNullable.group({
      screen: ['', Validators.required],
      startTime: ['', [Validators.required, minLeadTime(48)]],
      endTime: ['', Validators.required],
    }, { validators: endAfterStart('startTime', 'endTime') });
  }

  protected addShowtime(): void {
    this.showtimes.push(this.showtimeGroup());
  }

  protected removeShowtime(index: number): void {
    this.showtimes.removeAt(index);
    this.refreshScreenDerivedInfo();
  }

  private categoryGroup() {
    return this.fb.nonNullable.group({
      name: ['', [Validators.required, notBlank, Validators.minLength(2), Validators.maxLength(100)]],
      seatType: ['', Validators.required],
      price: [0, [Validators.required, Validators.min(0), Validators.max(100000)]],
    });
  }

  protected addCategory(): void {
    this.categories.push(this.categoryGroup());
  }

  protected removeCategory(index: number): void {
    this.categories.removeAt(index);
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const id = this.eventId();

    if (id !== null) {
      this.eventService.update(id, {
        title: v.title, description: v.description, startTime: v.startTime,
        endTime: v.endTime, imageUrl: v.imageUrl, category: v.category, screen: v.screen,
      }).subscribe({
        next: () => { this.toast.success('Event updated.'); this.router.navigate(['/organizer/events']); },
        error: (m: string) => this.toast.error(m),
      });
      return;
    }

    if (this.showtimes.length === 0) {
      this.toast.error('Add at least one showtime.');
      return;
    }
    if (this.categories.length === 0) {
      this.toast.error('Add at least one ticket category.');
      return;
    }
    const seatTypes = this.categories.controls.map(c => c.controls.seatType.value);
    if (new Set(seatTypes).size !== seatTypes.length) {
      this.toast.error('Each seat type can back only one ticket category.');
      return;
    }
    const overlapScreen = this.findScreenOverlap();
    if (overlapScreen) {
      this.toast.error(`Screen "${overlapScreen}" has overlapping showtimes.`);
      return;
    }

    const req: CreateEventWithScreeningsRequest = {
      venueId: v.venueId, title: v.title, description: v.description,
      imageUrl: v.imageUrl, category: v.category,
      showtimes: this.showtimes.controls.map(c => {
        const cv = c.getRawValue();
        return { screen: cv.screen, startTime: cv.startTime, endTime: cv.endTime };
      }),
      ticketCategories: this.categories.controls.map(c => {
        const cv = c.getRawValue();
        return { name: cv.name, seatType: cv.seatType, price: cv.price };
      }),
    };
    this.eventService.createWithScreenings(req).subscribe({
      next: () => { this.toast.success('Event created.'); this.router.navigate(['/organizer/events']); },
      error: (m: string) => this.toast.error(m),
    });
  }

  // A screen can't run two overlapping showtimes. datetime-local strings sort chronologically,
  // so a plain string compare is enough to spot an overlap. Returns the first clashing screen.
  private findScreenOverlap(): string | null {
    const byScreen = new Map<string, { start: string; end: string }[]>();
    for (const c of this.showtimes.controls) {
      const { screen, startTime, endTime } = c.getRawValue();
      if (!screen || !startTime || !endTime) continue;
      const list = byScreen.get(screen) ?? [];
      list.push({ start: startTime, end: endTime });
      byScreen.set(screen, list);
    }
    for (const [screen, times] of byScreen) {
      const sorted = [...times].sort((a, b) => a.start.localeCompare(b.start));
      for (let i = 1; i < sorted.length; i++)
        if (sorted[i].start < sorted[i - 1].end) return screen;
    }
    return null;
  }
}
