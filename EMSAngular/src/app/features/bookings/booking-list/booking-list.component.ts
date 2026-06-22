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
    <p class="eyebrow text-plum">Your tickets</p>
    <h1 class="page-title mt-2 mb-6">My bookings</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="space-y-3">
      <a *ngFor="let b of bookings()" [routerLink]="['/bookings', b.id]"
         class="flex items-center justify-between gap-4 rounded-2xl border border-line bg-surface p-5 transition hover:-translate-y-0.5 hover:shadow-card">
        <div>
          <p class="font-display text-lg font-semibold text-ink">{{ b.eventTitle }}</p>
          <p class="font-mono text-xs text-muted">{{ b.bookingReference }} · {{ b.createdAt | istDate }}</p>
        </div>
        <div class="text-right">
          <span class="badge"
                [class.bg-teal-tint]="b.bookingStatus === 'Confirmed'" [class.text-teal-dark]="b.bookingStatus === 'Confirmed'"
                [class.bg-gold-tint]="b.bookingStatus === 'Pending'" [class.text-gold]="b.bookingStatus === 'Pending'"
                [class.bg-rose-tint]="b.bookingStatus === 'Cancelled'" [class.text-rose-dark]="b.bookingStatus === 'Cancelled'"
                [class.bg-paper]="b.bookingStatus === 'Attended'" [class.text-ink-soft]="b.bookingStatus === 'Attended'">
            {{ b.bookingStatus }}
          </span>
          <p class="mt-1.5 font-mono text-sm text-ink">{{ b.totalAmount | inr }}</p>
        </div>
      </a>
      <div *ngIf="bookings().length === 0" class="card px-6 py-16 text-center">
        <p class="font-display text-xl text-ink">No bookings yet</p>
        <p class="mt-1 text-sm text-muted">When you book an event, your tickets show up here.</p>
        <a routerLink="/events" class="btn-primary mt-5">Browse events</a>
      </div>
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
