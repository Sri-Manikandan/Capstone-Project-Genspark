import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { EventService } from '../../../core/services/event.service';
import { BookingDto } from '../../../core/models/booking.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-bookings',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/organizer/events" class="link-action">← Back to my events</a>
    <p class="eyebrow text-plum mt-4">Organizer</p>
    <h1 class="page-title mt-2 mb-6">Bookings<span *ngIf="eventTitle()"> · {{ eventTitle() }}</span></h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="overflow-x-auto">
      <table class="data-table">
        <thead>
          <tr><th>Reference</th><th>Status</th><th>Tickets</th><th>Amount</th><th>Booked</th></tr>
        </thead>
        <tbody>
          <tr *ngFor="let b of bookings()">
            <td class="font-mono text-xs text-ink">{{ b.bookingReference }}</td>
            <td><span class="badge bg-paper text-ink-soft">{{ b.bookingStatus }}</span></td>
            <td>{{ b.items.length }}</td>
            <td class="font-mono">₹{{ b.totalAmount }}</td>
            <td class="font-mono text-xs">{{ b.createdAt | istDate }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p *ngIf="!loading() && bookings().length === 0" class="card mt-2 px-6 py-16 text-center text-muted">No bookings yet.</p>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class EventBookingsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private bookingService = inject(BookingService);
  private eventService = inject(EventService);

  protected bookings = signal<BookingDto[]>([]);
  protected eventTitle = signal('');
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);

  private eventId = 0;

  ngOnInit(): void {
    this.eventId = Number(this.route.snapshot.paramMap.get('id'));
    this.eventService.getById(this.eventId).subscribe({ next: ev => this.eventTitle.set(ev.title), error: () => {} });
    this.load();
  }

  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.bookingService.getByEvent(this.eventId, { page: this.page(), pageSize: 10 }).subscribe({
      next: res => { this.bookings.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
