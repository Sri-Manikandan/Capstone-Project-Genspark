import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { EventService } from '../../../core/services/event.service';
import { EventDto } from '../../../core/models/event.model';
import { EventCardComponent } from '../../../shared/components/event-card/event-card.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-event-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, EventCardComponent, PaginationComponent, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Upcoming Events</h1>
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-6 flex flex-wrap gap-3">
      <input formControlName="query" placeholder="Search events…" class="flex-1 rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="category" placeholder="Category" class="w-40 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Search</button>
    </form>

    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
      <ems-event-card *ngFor="let ev of events()" [event]="ev" />
    </div>
    <p *ngIf="!loading() && events().length === 0" class="py-10 text-center text-gray-500">No events found.</p>

    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class EventListComponent implements OnInit {
  private eventService = inject(EventService);
  private fb = inject(FormBuilder);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);
  protected filters = this.fb.nonNullable.group({ query: '', category: '' });

  ngOnInit(): void { this.load(); }

  protected applyFilters(): void { this.page.set(1); this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    const { query, category } = this.filters.getRawValue();
    this.eventService.search({ query, category, page: this.page(), pageSize: 9 }).subscribe({
      next: res => {
        this.events.set(res.items);
        this.totalPages.set(res.totalPages);
        this.loading.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }
}
