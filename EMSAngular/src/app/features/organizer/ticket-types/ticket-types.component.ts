import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { ScreeningService } from '../../../core/services/screening.service';
import { EventService } from '../../../core/services/event.service';
import { SeatService } from '../../../core/services/seat.service';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';
import { endAfterStart, notAfter, notBlank } from '../../../shared/validators/form-validators';

@Component({
  selector: 'ems-ticket-types',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent, FieldErrorComponent, LoadingSpinnerComponent, CurrencyInrPipe, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-types.component.html',
})
export class TicketTypesComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private service = inject(TicketTypeService);
  private screeningService = inject(ScreeningService);
  private eventService = inject(EventService);
  private seatService = inject(SeatService);

  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected seatTypes = signal<string[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected screeningId = Number(this.route.snapshot.paramMap.get('screeningId'));
  protected eventId = signal<number | null>(null);
  protected screenLabel = signal('');
  // Sale must close before the screening starts; the edge is fetched, so validators read it lazily.
  protected screeningStart = signal('');

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, notBlank, Validators.minLength(2), Validators.maxLength(100)]],
    seatType: ['', [Validators.required, Validators.minLength(2)]],
    price: [0, [Validators.required, Validators.min(0), Validators.max(100000)]],
    saleStart: ['', Validators.required],
    saleEnd: ['', [Validators.required, notAfter(() => this.screeningStart(), 'saleAfterScreening')]],
  }, { validators: endAfterStart('saleStart', 'saleEnd') });

  ngOnInit(): void {
    this.screeningService.getById(this.screeningId).subscribe({
      next: s => {
        this.eventId.set(s.eventId);
        this.screenLabel.set(s.screen);
        // Screening start bounds the sale window; re-run saleEnd's validator now the edge is known.
        this.screeningStart.set(s.startTime.slice(0, 16));
        this.form.controls.saleEnd.updateValueAndValidity();
        // Quantity maps to seat-type capacity, so offer the venue's seat types as a
        // dropdown instead of free text.
        this.eventService.getById(s.eventId).subscribe({
          next: ev => this.seatService.getByVenue(ev.venueId).subscribe({
            next: seats => this.seatTypes.set([...new Set(seats.map(x => x.seatType))].sort()),
            error: (m: string) => this.error.set(m),
          }),
          error: (m: string) => this.error.set(m),
        });
      },
      error: (m: string) => this.error.set(m),
    });
    this.load();
  }

  // Each seat type backs only one ticket type per screening; hide already-used types.
  protected availableSeatTypes(): string[] {
    const used = new Set(this.ticketTypes().map(t => t.seatType));
    return this.seatTypes().filter(t => !used.has(t));
  }

  protected add(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.service.create({ screeningId: this.screeningId, ...this.form.getRawValue() }).subscribe({
      next: () => { this.form.reset(); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.service.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.service.getByScreening(this.screeningId).subscribe({
      next: t => { this.ticketTypes.set(t); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
