import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { ScreeningService } from '../../../core/services/screening.service';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

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

  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected screeningId = Number(this.route.snapshot.paramMap.get('screeningId'));
  protected eventId = signal<number | null>(null);
  protected screenLabel = signal('');

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    seatType: ['', [Validators.required, Validators.minLength(2)]],
    price: [0, [Validators.min(0)]],
    totalQuantity: [1, [Validators.min(1)]],
    saleStart: ['', Validators.required],
    saleEnd: ['', Validators.required],
  });

  ngOnInit(): void {
    this.screeningService.getById(this.screeningId).subscribe({
      next: s => { this.eventId.set(s.eventId); this.screenLabel.set(s.screen); },
      error: (m: string) => this.error.set(m),
    });
    this.load();
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
