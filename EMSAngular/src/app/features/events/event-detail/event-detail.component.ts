import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
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

type Step = 'intro' | 'quantity' | 'seats';
interface SelectedSeat { reservation: SeatReservationDto; seat: SeatDto; ticketType: TicketTypeDto; }

@Component({
  selector: 'ems-event-detail',
  standalone: true,
  imports: [CommonModule, SeatMapComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <article *ngIf="event() as ev" class="space-y-8" [class.pb-24]="step() === 'seats'">
      <div class="relative overflow-hidden rounded-2xl">
        <img [src]="ev.imageUrl" [alt]="ev.title" class="aspect-[5/2] w-full object-cover" />
        <div class="absolute inset-0 bg-gradient-to-t from-ink/80 via-ink/20 to-transparent"></div>
        <header class="absolute inset-x-0 bottom-0 p-6 sm:p-8">
          <span class="eyebrow text-white/80">{{ ev.category }}</span>
          <h1 class="mt-1 font-display text-3xl font-semibold leading-tight text-white sm:text-4xl">{{ ev.title }}</h1>
          <p class="mt-2 font-mono text-sm text-white/90">{{ ev.startTime | istDate }}</p>
        </header>
      </div>

      <!-- ── Step 1 · Intro ─────────────────────────────────────────── -->
      <ng-container *ngIf="step() === 'intro'">
        <p class="max-w-2xl text-base leading-relaxed text-ink-soft">{{ ev.description }}</p>

        <section *ngIf="ticketTypes().length">
          <h2 class="section-title mb-3">Ticket categories</h2>
          <div class="flex flex-wrap gap-3">
            <div *ngFor="let t of ticketTypes()" class="rounded-2xl border border-line bg-surface px-5 py-3">
              <span class="block font-display text-lg font-semibold text-ink">{{ t.name }}</span>
              <span class="block font-mono text-xs text-ink-soft">{{ t.price | inr }} · {{ t.availableQuantity }} left</span>
            </div>
          </div>
        </section>

        <button type="button" (click)="proceedToBook()" class="btn-primary">Proceed to Book</button>
      </ng-container>

      <!-- ── Step 2 · Quantity ──────────────────────────────────────── -->
      <ng-container *ngIf="step() === 'quantity'">
        <section class="card max-w-md p-6">
          <p class="eyebrow text-plum">Step 1 of 2</p>
          <h2 class="section-title mt-2 mb-1">How many tickets?</h2>
          <p class="mb-6 text-sm text-ink-soft">You'll pick the exact seats next — mix categories if you like.</p>

          <div class="flex items-center gap-5">
            <button type="button" (click)="changeQuantity(-1)" [disabled]="quantity() <= 1"
                    class="btn-ghost h-11 w-11 p-0 text-2xl leading-none" aria-label="Fewer tickets">−</button>
            <span class="w-12 text-center font-display text-4xl font-semibold text-ink">{{ quantity() }}</span>
            <button type="button" (click)="changeQuantity(1)" [disabled]="quantity() >= maxQuantity"
                    class="btn-ghost h-11 w-11 p-0 text-2xl leading-none" aria-label="More tickets">+</button>
          </div>

          <div class="mt-7 flex items-center gap-3">
            <button type="button" (click)="continueToSeats()" class="btn-primary">Choose seats</button>
            <button type="button" (click)="step.set('intro')" class="btn-ghost">Back</button>
          </div>
        </section>
      </ng-container>

      <!-- ── Step 3 · Seats ─────────────────────────────────────────── -->
      <ng-container *ngIf="step() === 'seats'">
        <section>
          <div class="mb-3 flex flex-wrap items-end justify-between gap-2">
            <div>
              <p class="eyebrow text-plum">Step 2 of 2</p>
              <h2 class="section-title mt-1">Select your seats</h2>
            </div>
            <button type="button" (click)="step.set('quantity')" class="link-action">Change quantity ({{ quantity() }})</button>
          </div>
          <p class="mb-4 text-sm text-ink-soft">Pick {{ quantity() }} seat{{ quantity() === 1 ? '' : 's' }} — the section sets the category and price.</p>
          <div class="card p-5">
            <ems-seat-map [eventId]="ev.id" [venueId]="ev.venueId" [ticketTypes]="ticketTypes()"
                          [screenName]="ev.screen ?? ''"
                          [selectedSeatIds]="selectedSeatIds()" (seatToggled)="onSeatToggled($event)" />
          </div>
        </section>

        <section *ngIf="selected().length" class="card p-5">
          <h3 class="eyebrow mb-3">Your selection</h3>
          <ul class="space-y-2 text-sm text-ink-soft">
            <li *ngFor="let s of selected()" class="flex items-center justify-between gap-4">
              <span><span class="font-mono text-ink">{{ s.seat.section }}-{{ s.seat.row }}-{{ s.seat.seatNumber }}</span>
                <span class="text-muted"> · {{ s.ticketType.name }}</span></span>
              <span class="font-mono">{{ s.ticketType.price | inr }}</span>
            </li>
          </ul>
        </section>
      </ng-container>

      <!-- ── Sticky checkout bar (seats step) ───────────────────────── -->
      <div *ngIf="step() === 'seats'" class="fixed inset-x-0 bottom-0 z-20 border-t border-line bg-paper/90 backdrop-blur">
        <div class="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3.5">
          <span class="font-mono text-sm text-ink-soft">
            {{ selected().length }} of {{ quantity() }} · {{ total() | inr }}
          </span>
          <button [disabled]="selected().length !== quantity()" (click)="checkout()" class="btn-primary">
            Proceed to Checkout
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

  protected readonly maxQuantity = 10;

  protected event = signal<EventDto | null>(null);
  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected step = signal<Step>('intro');
  protected quantity = signal(1);
  protected selected = signal<SelectedSeat[]>([]);

  protected selectedSeatIds = computed(() => this.selected().map(s => s.seat.id));
  protected total = computed(() => this.selected().reduce((sum, s) => sum + s.ticketType.price, 0));

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

  protected proceedToBook(): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: this.router.url } });
      return;
    }
    this.step.set('quantity');
  }

  protected changeQuantity(delta: number): void {
    this.quantity.update(q => Math.min(this.maxQuantity, Math.max(1, q + delta)));
  }

  protected continueToSeats(): void {
    this.step.set('seats');
  }

  protected onSeatToggled(seat: SeatDto): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: this.router.url } });
      return;
    }

    const existing = this.selected().find(s => s.seat.id === seat.id);
    if (existing) {
      this.seatService.releaseReservation(existing.reservation.id).subscribe({
        next: () => this.selected.update(list => list.filter(s => s.seat.id !== seat.id)),
        error: (msg: string) => this.error.set(msg),
      });
      return;
    }

    if (this.selected().length >= this.quantity()) {
      this.error.set(`You've already picked ${this.quantity()} seat${this.quantity() === 1 ? '' : 's'}. Deselect one to swap.`);
      return;
    }

    const ticketType = this.ticketTypes().find(
      t => t.seatType.toLowerCase() === seat.seatType.toLowerCase());
    if (!ticketType) { this.error.set('No ticket category is available for this section.'); return; }

    this.seatService.reserve({ eventId: this.event()!.id, seatId: seat.id, ticketTypeId: ticketType.id }).subscribe({
      next: reservation => this.selected.update(list => [...list, { reservation, seat, ticketType }]),
      error: (msg: string) => this.error.set(msg),
    });
  }

  protected checkout(): void {
    const items = this.selected().map(s => ({ ticketTypeId: s.ticketType.id, seatId: s.seat.id }));
    this.bookingService.create({ eventId: this.event()!.id, items }).subscribe({
      next: booking => this.router.navigate(['/checkout', booking.id]),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
