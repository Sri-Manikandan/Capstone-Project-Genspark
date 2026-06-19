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
      <section class="rounded-lg border border-gray-200 bg-white p-6">
        <h1 class="text-xl font-semibold text-gray-900">{{ b.eventTitle }}</h1>
        <p class="text-sm text-gray-500">{{ b.bookingReference }} · {{ b.createdAt | istDate }}</p>
        <ul class="mt-4 space-y-1 text-sm text-gray-700">
          <li *ngFor="let item of b.items" class="flex justify-between">
            <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
            <span>{{ item.unitPrice | inr }}</span>
          </li>
        </ul>
        <div class="mt-3 flex justify-between border-t border-gray-200 pt-3 font-semibold text-gray-900">
          <span>Total</span><span>{{ b.totalAmount | inr }}</span>
        </div>
        <button *ngIf="canCancel(b)" (click)="cancel(b.id)"
                class="mt-4 rounded-lg bg-red-600 px-4 py-2 text-white hover:bg-red-700">Cancel booking</button>
      </section>

      <section class="flex items-center justify-center rounded-lg border border-gray-200 bg-white p-6">
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
