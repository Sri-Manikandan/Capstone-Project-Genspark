import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { SeatService } from '../../../core/services/seat.service';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { EventDto } from '../../../core/models/event.model';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { SeatDto, SeatReservationDto } from '../../../core/models/seat.model';
import { SeatMapComponent } from '../../../shared/components/seat-map/seat-map.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-event-detail',
  standalone: true,
  imports: [CommonModule, SeatMapComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <article *ngIf="event() as ev" class="space-y-6">
      <img [src]="ev.imageUrl" [alt]="ev.title" class="aspect-[3/1] w-full rounded-lg object-cover" />
      <header>
        <span class="text-xs font-medium text-indigo-600">{{ ev.category }}</span>
        <h1 class="text-2xl font-semibold text-gray-900">{{ ev.title }}</h1>
        <p class="text-gray-600">{{ ev.startTime | istDate }}</p>
      </header>
      <p class="text-gray-700">{{ ev.description }}</p>

      <section>
        <h2 class="mb-2 text-lg font-semibold text-gray-900">Tickets</h2>
        <div class="flex flex-wrap gap-3">
          <button *ngFor="let t of ticketTypes()" type="button"
                  (click)="activeTicketTypeId.set(t.id)"
                  class="rounded-lg border px-4 py-2 text-left"
                  [class.border-indigo-600]="activeTicketTypeId() === t.id"
                  [class.border-gray-300]="activeTicketTypeId() !== t.id">
            <span class="block font-medium text-gray-900">{{ t.name }}</span>
            <span class="block text-sm text-gray-600">{{ t.price | inr }} · {{ t.availableQuantity }} left</span>
          </button>
        </div>
      </section>

      <section *ngIf="activeTicketTypeId()">
        <h2 class="mb-2 text-lg font-semibold text-gray-900">Select seats</h2>
        <ems-seat-map [eventId]="ev.id" [venueId]="ev.venueId"
                      [selectedSeatIds]="selectedSeatIds()" (seatToggled)="onSeatToggled($event)" />
      </section>

      <div class="flex items-center justify-between border-t border-gray-200 pt-4">
        <span class="text-gray-700">{{ selected().length }} seat(s) selected</span>
        <button [disabled]="selected().length === 0" (click)="checkout()"
                class="rounded-lg bg-indigo-600 px-5 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">
          Proceed to Checkout
        </button>
      </div>
    </article>
  `,
})
export class EventDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private eventService = inject(EventService);
  private ticketTypeService = inject(TicketTypeService);
  private seatService = inject(SeatService);
  private bookingService = inject(BookingService);
  private auth = inject(AuthService);

  protected event = signal<EventDto | null>(null);
  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected activeTicketTypeId = signal<number | null>(null);
  protected selected = signal<SeatReservationDto[]>([]);

  protected selectedSeatIds = () => this.selected().map(r => r.seatId);

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug')!;
    this.loading.set(true);
    this.eventService.getBySlug(slug).subscribe({
      next: ev => {
        this.event.set(ev);
        this.loading.set(false);
        this.ticketTypeService.getActiveByEvent(ev.id).subscribe({
          next: tts => this.ticketTypes.set(tts),
          error: (msg: string) => this.error.set(msg),
        });
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected onSeatToggled(seat: SeatDto): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: this.router.url } });
      return;
    }
    const ttId = this.activeTicketTypeId();
    if (!ttId) { this.error.set('Pick a ticket type first.'); return; }

    const existing = this.selected().find(r => r.seatId === seat.id);
    if (existing) {
      this.seatService.releaseReservation(existing.id).subscribe({
        next: () => this.selected.update(list => list.filter(r => r.seatId !== seat.id)),
        error: (msg: string) => this.error.set(msg),
      });
      return;
    }
    this.seatService.reserve({ eventId: this.event()!.id, seatId: seat.id, ticketTypeId: ttId }).subscribe({
      next: res => this.selected.update(list => [...list, res]),
      error: (msg: string) => this.error.set(msg),
    });
  }

  protected checkout(): void {
    const items = this.selected().map(r => ({ ticketTypeId: r.ticketTypeId, seatId: r.seatId }));
    this.bookingService.create({ eventId: this.event()!.id, items }).subscribe({
      next: booking => this.router.navigate(['/checkout', booking.id]),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
