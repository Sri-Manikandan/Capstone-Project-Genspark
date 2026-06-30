import { Injectable, computed, inject, signal } from '@angular/core';
import { EventSearchRequest } from '../../core/models/event.model';
import { EventService } from '../../core/services/event.service';
import { createFilterStore } from '../../core/state/filter-store';

export interface EventFilters {
  query: string;
  category: string;
  sortBy: 'startTime' | 'title' | 'createdAt';
  sortOrder: 'asc' | 'desc';
  startFrom: string;
  startTo: string;
}

const INITIAL: EventFilters = {
  query: '',
  category: '',
  sortBy: 'startTime',
  sortOrder: 'asc',
  startFrom: '',
  startTo: '',
};

@Injectable({ providedIn: 'root' })
export class EventFilterStore {
  private eventService = inject(EventService);
  private store = createFilterStore<EventFilters>({ initial: INITIAL, pageSize: 9 });

  readonly filters = this.store.filters;
  readonly page = this.store.page;
  readonly categories = signal<string[]>([]);

  readonly request = computed<EventSearchRequest>(() => {
    const f = this.store.filters();
    return {
      query: f.query || undefined,
      category: f.category || undefined,
      startFrom: f.startFrom || undefined,
      startTo: f.startTo || undefined,
      sortBy: f.sortBy,
      sortOrder: f.sortOrder,
      page: this.store.page(),
      pageSize: this.store.pageSize,
    };
  });

  patch = this.store.patch;
  setPage = this.store.setPage;
  reset = this.store.reset;

  loadCategories(): void {
    if (this.categories().length > 0) return;
    this.eventService.getCategories().subscribe({
      next: cats => this.categories.set(cats),
      error: () => this.categories.set([]),
    });
  }
}
