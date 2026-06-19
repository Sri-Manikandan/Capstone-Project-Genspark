import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto } from '../../../core/models/booking.model';
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
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">My Bookings</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="space-y-3">
      <a *ngFor="let b of bookings()" [routerLink]="['/bookings', b.id]"
         class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-4 hover:shadow-sm">
        <div>
          <p class="font-medium text-gray-900">{{ b.eventTitle }}</p>
          <p class="text-sm text-gray-500">{{ b.bookingReference }} · {{ b.createdAt | istDate }}</p>
        </div>
        <div class="text-right">
          <span class="rounded-full px-2 py-0.5 text-xs font-medium"
                [class.bg-green-50]="b.bookingStatus === 'Confirmed'" [class.text-green-700]="b.bookingStatus === 'Confirmed'"
                [class.bg-amber-50]="b.bookingStatus === 'Pending'" [class.text-amber-700]="b.bookingStatus === 'Pending'"
                [class.bg-red-50]="b.bookingStatus === 'Cancelled'" [class.text-red-700]="b.bookingStatus === 'Cancelled'"
                [class.bg-gray-100]="b.bookingStatus === 'Attended'" [class.text-gray-700]="b.bookingStatus === 'Attended'">
            {{ b.bookingStatus }}
          </span>
          <p class="mt-1 text-sm text-gray-900">{{ b.totalAmount | inr }}</p>
        </div>
      </a>
      <p *ngIf="bookings().length === 0" class="py-10 text-center text-gray-500">No bookings yet.</p>
    </div>

    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class BookingListComponent implements OnInit {
  private bookingService = inject(BookingService);

  protected bookings = signal<BookingDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);

  ngOnInit(): void { this.load(); }

  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.bookingService.getMyBookings({ page: this.page(), pageSize: 10 }).subscribe({
      next: res => { this.bookings.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }
}
