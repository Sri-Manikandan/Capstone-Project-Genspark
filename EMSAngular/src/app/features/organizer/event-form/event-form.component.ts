import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { SeatService } from '../../../core/services/seat.service';
import { ScreeningService } from '../../../core/services/screening.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { ToastService } from '../../../core/services/toast.service';
import { VenueDto } from '../../../core/models/venue.model';
import { EventDto } from '../../../core/models/event.model';
import { EVENT_CATEGORIES } from '../../../core/constants/event-categories';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { endAfterStart, httpUrl, minLeadTime, notBlank, selectRequired } from '../../../shared/validators/form-validators';
import { istNowWallClock } from '../../../shared/date/ist-now';
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
  private screeningService = inject(ScreeningService);
  private ticketTypeService = inject(TicketTypeService);
  private toast = inject(ToastService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // Signal so an edited event whose stored category predates this list still shows selected.
  protected categoryOptions = signal<string[]>([...EVENT_CATEGORIES]);
  protected venues = signal<VenueDto[]>([]);
  protected screens = signal<string[]>([]);
  protected seatTypes = signal<string[]>([]);
  // Seat-type → number of seats of that type in the venue; a ticket category's
  // quantity is fixed to this and shown read-only.
  protected seatTypeCounts = signal<Record<string, number>>({});
  protected eventId = signal<number | null>(null);
  protected isEdit = computed(() => this.eventId() !== null);

  form = this.fb.nonNullable.group({
    venueId: [0, [selectRequired]],
    screen: [''],
    title: ['', [Validators.required, notBlank, Validators.minLength(2), Validators.maxLength(200)]],
    description: ['', [Validators.required, notBlank, Validators.minLength(1), Validators.maxLength(2000)]],
    startTime: ['', [Validators.required, minLeadTime(48)]],
    endTime: ['', Validators.required],
    imageUrl: ['', [Validators.required, httpUrl]],
    category: ['', [Validators.required]],
    // New events collect their ticket categories inline; they're saved against the
    // default screening the API auto-creates. Empty (and ignored) when editing.
    ticketTypes: this.fb.array<ReturnType<EventFormComponent['categoryGroup']>>([]),
  }, { validators: endAfterStart('startTime', 'endTime') });

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
      // Start new events with one blank ticket category so the section is never empty.
      this.addCategory();
    }
  }

  protected onVenueChange(venueId: number): void {
    if (!venueId) { this.screens.set([]); this.seatTypes.set([]); this.seatTypeCounts.set({}); return; }
    this.seatService.getByVenue(venueId).subscribe({
      next: seats => {
        this.screens.set([...new Set(seats.map(s => s.section))].sort());
        this.seatTypes.set([...new Set(seats.map(s => s.seatType))].sort());
        this.seatTypeCounts.set(seats.reduce<Record<string, number>>(
          (acc, s) => { acc[s.seatType] = (acc[s.seatType] ?? 0) + 1; return acc; }, {}));
      },
      // Surface the failure instead of silently blanking the lists, which would otherwise
      // look identical to "this venue has no seats yet".
      error: (m: string) => { this.screens.set([]); this.seatTypes.set([]); this.seatTypeCounts.set({}); this.toast.error(m); },
    });
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

  protected capacityFor(seatType: string): number {
    return this.seatTypeCounts()[seatType] ?? 0;
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

    if (this.categories.length === 0) {
      this.toast.error('Add at least one ticket category.');
      return;
    }
    const seatTypes = this.categories.controls.map(c => c.controls.seatType.value);
    if (new Set(seatTypes).size !== seatTypes.length) {
      this.toast.error('Each seat type can back only one ticket category.');
      return;
    }
    this.eventService.create({
      venueId: v.venueId, title: v.title, description: v.description, startTime: v.startTime,
      endTime: v.endTime, imageUrl: v.imageUrl, category: v.category, screen: v.screen,
    }).subscribe({
      next: ev => this.createTicketTypes(ev, v.startTime),
      error: (m: string) => this.toast.error(m),
    });
  }

  // Ticket types hang off a screening, so we grab the default screening the API seeds
  // for every new event, then create each category against it.
  private createTicketTypes(event: EventDto, eventStart: string): void {
    this.screeningService.getByEvent(event.id).subscribe({
      next: screenings => {
        const screening = screenings[0];
        if (!screening) {
          this.toast.error('Event created, but no screening was found to attach tickets to.');
          this.router.navigate(['/organizer/events']);
          return;
        }
        const saleStart = istNowWallClock();
        const requests = this.categories.controls.map(c => {
          const cv = c.getRawValue();
          return this.ticketTypeService.create({
            screeningId: screening.id, name: cv.name, seatType: cv.seatType,
            price: cv.price, saleStart, saleEnd: eventStart,
          });
        });
        forkJoin(requests).subscribe({
          next: () => { this.toast.success('Event created.'); this.router.navigate(['/organizer/events']); },
          error: (m: string) => this.toast.error(`Event created, but a ticket category failed: ${m}`),
        });
      },
      error: (m: string) => this.toast.error(m),
    });
  }
}
