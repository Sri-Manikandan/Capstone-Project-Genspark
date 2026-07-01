import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { SeatService } from '../../../core/services/seat.service';
import { BookingService } from '../../../core/services/booking.service';
import { newIdempotencyKey } from '../../../core/services/idempotency-key';
import { AuthService } from '../../../core/services/auth.service';
import { EventDto } from '../../../core/models/event.model';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { SeatDto, SeatReservationDto } from '../../../core/models/seat.model';
import { SeatMapComponent } from '../../../shared/components/seat-map/seat-map.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';
import { TicketPickerComponent } from './ticket-picker/ticket-picker.component';

type Step = 'intro' | 'seats';
interface SelectedSeat { reservation: SeatReservationDto; seat: SeatDto; ticketType: TicketTypeDto; }

@Component({
  selector: 'ems-event-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, SeatMapComponent, TicketPickerComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-detail.component.html',
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
  protected showPicker = signal(false);
  protected selectedTicketType = signal<TicketTypeDto | null>(null);
  protected quantity = signal(1);
  protected selected = signal<SelectedSeat[]>([]);

  // Stable across retries of the same booking attempt so a double-submit can't double-book.
  private bookingKey = '';

  protected selectedSeatIds = computed(() => this.selected().map(s => s.seat.id));
  protected total = computed(() => this.selected().reduce((sum, s) => sum + s.ticketType.price, 0));
  protected restrictToSeatType = computed(() => this.selectedTicketType()?.seatType ?? null);

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
    this.showPicker.set(true);
  }

  protected closePicker(): void {
    this.showPicker.set(false);
  }

  protected onPickerConfirm(choice: { ticketType: TicketTypeDto; quantity: number }): void {
    // Changing the category or quantity restarts seat selection — drop any held seats.
    this.releaseAllSeats();
    this.selectedTicketType.set(choice.ticketType);
    this.quantity.set(choice.quantity);
    this.showPicker.set(false);
    this.step.set('seats');
  }

  protected onSeatToggled(seat: SeatDto): void {
    const ticketType = this.selectedTicketType();
    if (!ticketType) return;

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

    this.seatService.reserve({ eventId: this.event()!.id, seatId: seat.id, ticketTypeId: ticketType.id }).subscribe({
      next: reservation => this.selected.update(list => [...list, { reservation, seat, ticketType }]),
      error: (msg: string) => this.error.set(msg),
    });
  }

  private releaseAllSeats(): void {
    for (const s of this.selected()) {
      this.seatService.releaseReservation(s.reservation.id).subscribe({ error: () => {} });
    }
    this.selected.set([]);
  }

  protected checkout(): void {
    const items = this.selected().map(s => ({ ticketTypeId: s.ticketType.id, seatId: s.seat.id }));
    if (!this.bookingKey) this.bookingKey = newIdempotencyKey();
    this.bookingService.create({ eventId: this.event()!.id, items }, this.bookingKey).subscribe({
      next: booking => this.router.navigate(['/checkout', booking.id]),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
