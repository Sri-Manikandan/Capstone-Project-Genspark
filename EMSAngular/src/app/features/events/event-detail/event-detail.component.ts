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

    <article *ngIf="event() as ev" class="space-y-8 pb-24">
      <div class="relative overflow-hidden rounded-2xl">
        <img [src]="ev.imageUrl" [alt]="ev.title" class="aspect-[5/2] w-full object-cover" />
        <div class="absolute inset-0 bg-gradient-to-t from-ink/80 via-ink/20 to-transparent"></div>
        <header class="absolute inset-x-0 bottom-0 p-6 sm:p-8">
          <span class="eyebrow text-white/80">{{ ev.category }}</span>
          <h1 class="mt-1 font-display text-3xl font-semibold leading-tight text-white sm:text-4xl">{{ ev.title }}</h1>
          <p class="mt-2 font-mono text-sm text-white/90">{{ ev.startTime | istDate }}</p>
        </header>
      </div>

      <p class="max-w-2xl text-base leading-relaxed text-ink-soft">{{ ev.description }}</p>

      <section>
        <h2 class="section-title mb-3">Choose your ticket</h2>
        <div class="flex flex-wrap gap-3">
          <button *ngFor="let t of ticketTypes()" type="button"
                  (click)="activeTicketTypeId.set(t.id)"
                  class="rounded-2xl border-2 px-5 py-3 text-left transition"
                  [class.border-plum]="activeTicketTypeId() === t.id"
                  [class.bg-plum-tint]="activeTicketTypeId() === t.id"
                  [class.border-line]="activeTicketTypeId() !== t.id"
                  [class.bg-surface]="activeTicketTypeId() !== t.id">
            <span class="block font-display text-lg font-semibold text-ink">{{ t.name }}</span>
            <span class="block font-mono text-xs text-ink-soft">{{ t.price | inr }} · {{ t.availableQuantity }} left</span>
          </button>
        </div>
      </section>

      <section *ngIf="activeTicketTypeId()">
        <h2 class="section-title mb-3">Select your seats</h2>
        <div class="card p-5">
          <ems-seat-map [eventId]="ev.id" [venueId]="ev.venueId"
                        [selectedSeatIds]="selectedSeatIds()" (seatToggled)="onSeatToggled($event)" />
        </div>
      </section>

      <div class="fixed inset-x-0 bottom-0 z-20 border-t border-line bg-paper/90 backdrop-blur">
        <div class="mx-auto flex max-w-6xl items-center justify-between px-4 py-3.5">
          <span class="font-mono text-sm text-ink-soft">
            {{ selected().length }} seat{{ selected().length === 1 ? '' : 's' }} selected
          </span>
          <button [disabled]="selected().length === 0" (click)="checkout()" class="btn-primary">
            Proceed to checkout
          </button>
        </div>
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
