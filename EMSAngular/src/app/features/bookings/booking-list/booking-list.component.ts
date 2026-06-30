import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto, BookingQueryRequest, BookingStatus } from '../../../core/models/booking.model';
import { BookingFilterStore } from '../booking-filter.store';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-booking-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './booking-list.component.html',
})
export class BookingListComponent {
  private bookingService = inject(BookingService);
  protected store = inject(BookingFilterStore);

  protected bookings = signal<BookingDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected totalPages = signal(1);

  constructor() {
    effect(() => {
      const req = this.store.request();
      this.load(req);
    });
  }

  protected setStatus(status: string): void {
    this.store.patch({ status: status as BookingStatus | '' });
  }
  protected goToPage(p: number): void { this.store.setPage(p); }
  protected clearFilters(): void { this.store.reset(); }

  private load(req: BookingQueryRequest): void {
    this.loading.set(true);
    this.bookingService.getMyBookings(req).subscribe({
      next: res => { this.bookings.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }
}
