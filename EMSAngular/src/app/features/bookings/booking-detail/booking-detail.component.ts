import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto } from '../../../core/models/booking.model';
import { BookingQrComponent } from '../../../shared/components/booking-qr/booking-qr.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-booking-detail',
  standalone: true,
  imports: [CommonModule, BookingQrComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <article *ngIf="booking() as b" class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <section class="card p-6">
        <span class="eyebrow text-plum">Ticket</span>
        <h1 class="mt-2 font-display text-2xl font-semibold text-ink">{{ b.eventTitle }}</h1>
        <p class="mt-1 font-mono text-xs text-muted">{{ b.bookingReference }} · {{ b.createdAt | istDate }}</p>
        <ul class="mt-5 space-y-2 text-sm text-ink-soft">
          <li *ngFor="let item of b.items" class="flex justify-between gap-4">
            <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
            <span class="font-mono">{{ item.unitPrice | inr }}</span>
          </li>
        </ul>
        <div class="mt-5 flex items-center justify-between border-t border-dashed border-line pt-5">
          <span class="eyebrow">Total paid</span>
          <span class="font-display text-2xl font-semibold text-ink">{{ b.totalAmount | inr }}</span>
        </div>
        <button *ngIf="canCancel(b)" (click)="cancel(b.id)" class="btn-danger mt-6">Cancel booking</button>
      </section>

      <section class="card flex items-center justify-center p-6">
        <ems-booking-qr [qrCode]="b.qrCode" />
      </section>
    </article>
  `,
})
export class BookingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private bookingService = inject(BookingService);

  protected booking = signal<BookingDto | null>(null);
  protected loading = signal(false);
  protected error = signal('');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);
    this.bookingService.getById(id).subscribe({
      next: b => { this.booking.set(b); this.loading.set(false); },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected canCancel(b: BookingDto): boolean {
    return b.bookingStatus === 'Pending' || b.bookingStatus === 'Confirmed';
  }

  protected cancel(id: number): void {
    this.bookingService.cancel(id).subscribe({
      next: b => this.booking.set(b),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
