import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
  imports: [CommonModule, RouterLink, BookingQrComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './booking-detail.component.html',
})
export class BookingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private bookingService = inject(BookingService);

  protected booking = signal<BookingDto | null>(null);
  protected loading = signal(false);
  protected error = signal('');
  protected justConfirmed = signal(false);

  ngOnInit(): void {
    this.justConfirmed.set(this.route.snapshot.queryParamMap.get('confirmed') === '1');
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
