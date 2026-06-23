import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
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
    <section class="mb-10">
      <p class="eyebrow text-plum">What's on · Live this season</p>
      <h1 class="page-title mt-3 max-w-3xl text-balance">
        Find the night you'll <span class="italic text-plum">remember</span>.
      </h1>
      <p class="mt-4 max-w-xl text-base text-ink-soft">
        Concerts, conferences and shows — pick your seat and walk in with a ticket on your phone.
      </p>

      <form [formGroup]="filters" (ngSubmit)="applyFilters()"
            class="mt-7 flex flex-col gap-2 rounded-2xl border border-line bg-surface p-2 shadow-card sm:flex-row sm:items-center">
        <input formControlName="query" aria-label="Search events, artists, venues" placeholder="Search events, artists, venues…"
               class="flex-1 rounded-xl border-0 bg-transparent px-3 py-2.5 text-sm text-ink placeholder:text-muted/70 focus:outline-none focus:ring-0" />
        <input formControlName="category" aria-label="Filter by category" placeholder="Category"
               class="rounded-xl border-0 bg-paper px-3 py-2.5 text-sm text-ink placeholder:text-muted/70 focus:outline-none focus:ring-0 sm:w-44" />
        <button type="submit" class="btn-primary">Search</button>
      </form>
    </section>

    <div class="mb-5 flex items-baseline justify-between">
      <h2 class="section-title">Upcoming events</h2>
      <span class="eyebrow hidden sm:block">Page {{ page() }} of {{ totalPages() }}</span>
    </div>

    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
      <ems-event-card *ngFor="let ev of events()" [event]="ev" />
    </div>

    <div *ngIf="!loading() && events().length === 0" class="card mt-2 px-6 py-16 text-center">
      <p class="font-display text-xl text-ink">No events match your search</p>
      <p class="mt-1 text-sm text-muted">Try a different keyword or clear the category filter.</p>
    </div>

    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class EventListComponent implements OnInit {
  private eventService = inject(EventService);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);
  protected filters = this.fb.nonNullable.group({ query: '', category: '' });

  ngOnInit(): void {
    const category = this.route.snapshot.queryParamMap.get('category');
    if (category) this.filters.patchValue({ category });
    this.load();
  }

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
