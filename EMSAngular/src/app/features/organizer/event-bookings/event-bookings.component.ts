import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { EventService } from '../../../core/services/event.service';
import { BookingDto } from '../../../core/models/booking.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

@Component({
  selector: 'ems-event-bookings',
  standalone: true,
  imports: [CommonModule, PaginationComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-bookings.component.html',
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

  protected eventId = 0;

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
