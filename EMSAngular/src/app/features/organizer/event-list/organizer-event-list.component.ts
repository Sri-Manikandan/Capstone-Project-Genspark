import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { EventDto } from '../../../core/models/event.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-organizer-event-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-6 flex items-end justify-between gap-4">
      <div>
        <p class="eyebrow text-plum">Organizer</p>
        <h1 class="page-title mt-2">My events</h1>
      </div>
      <a routerLink="/organizer/events/new" class="btn-primary">+ New event</a>
    </div>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="overflow-x-auto">
      <table class="data-table">
        <thead>
          <tr><th>Title</th><th>Start</th><th>Status</th><th>Actions</th></tr>
        </thead>
        <tbody>
          <tr *ngFor="let ev of events()">
            <td class="font-medium text-ink">{{ ev.title }}</td>
            <td class="font-mono text-xs">{{ ev.startTime | istDate }}</td>
            <td><span class="badge bg-paper text-ink-soft">{{ ev.status }}</span></td>
            <td>
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
                <a [routerLink]="['/organizer/events', ev.id, 'edit']" class="link-action">Edit</a>
                <a [routerLink]="['/organizer/events', ev.id, 'tickets']" class="link-action">Tickets</a>
                <a [routerLink]="['/organizer/events', ev.id, 'orders']" class="link-action">Bookings</a>
                <a [routerLink]="['/organizer/events', ev.id, 'bookings']" class="link-action">Scan</a>
                <button *ngIf="ev.status === 'Draft' || ev.status === 'Rejected'" (click)="submitEvent(ev.id)" class="link-go">Submit</button>
                <button *ngIf="ev.status !== 'Cancelled'" (click)="cancelEvent(ev.id)" class="link-danger">Cancel</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p *ngIf="!loading() && events().length === 0" class="card mt-2 px-6 py-16 text-center text-muted">No events yet — create your first one.</p>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class OrganizerEventListComponent implements OnInit {
  private eventService = inject(EventService);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);

  ngOnInit(): void { this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected submitEvent(id: number): void {
    this.eventService.submit(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }
  protected cancelEvent(id: number): void {
    this.eventService.cancel(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.eventService.getMyEvents(this.page(), 10).subscribe({
      next: res => { this.events.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
