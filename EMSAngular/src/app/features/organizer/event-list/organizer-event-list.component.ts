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
    <div class="mb-4 flex items-center justify-between">
      <h1 class="text-2xl font-semibold text-gray-900">My Events</h1>
      <a routerLink="/organizer/events/new" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">New Event</a>
    </div>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <table *ngIf="!loading()" class="w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-sm">
      <thead class="bg-gray-50 text-left text-gray-600">
        <tr><th class="p-3">Title</th><th class="p-3">Start</th><th class="p-3">Status</th><th class="p-3">Actions</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let ev of events()" class="border-t border-gray-100">
          <td class="p-3 font-medium text-gray-900">{{ ev.title }}</td>
          <td class="p-3 text-gray-600">{{ ev.startTime | istDate }}</td>
          <td class="p-3 text-gray-600">{{ ev.status }}</td>
          <td class="p-3">
            <div class="flex flex-wrap gap-2">
              <a [routerLink]="['/organizer/events', ev.id, 'edit']" class="text-indigo-600 hover:underline">Edit</a>
              <a [routerLink]="['/organizer/events', ev.id, 'tickets']" class="text-indigo-600 hover:underline">Tickets</a>
              <a [routerLink]="['/organizer/events', ev.id, 'bookings']" class="text-indigo-600 hover:underline">Scan</a>
              <button *ngIf="ev.status === 'Draft' || ev.status === 'Rejected'" (click)="submitEvent(ev.id)" class="text-green-600 hover:underline">Submit</button>
              <button *ngIf="ev.status !== 'Cancelled'" (click)="cancelEvent(ev.id)" class="text-red-600 hover:underline">Cancel</button>
            </div>
          </td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!loading() && events().length === 0" class="py-10 text-center text-gray-500">No events yet.</p>
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
