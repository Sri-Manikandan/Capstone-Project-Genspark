import { Injectable, computed } from '@angular/core';
import { BookingQueryRequest, BookingStatus } from '../../core/models/booking.model';
import { createFilterStore } from '../../core/state/filter-store';

export interface BookingFilters {
  status: BookingStatus | '';
}

const INITIAL: BookingFilters = { status: '' };

@Injectable({ providedIn: 'root' })
export class BookingFilterStore {
  private store = createFilterStore<BookingFilters>({ initial: INITIAL, pageSize: 10 });

  readonly filters = this.store.filters;
  readonly page = this.store.page;

  readonly request = computed<BookingQueryRequest>(() => {
    const f = this.store.filters();
    return {
      status: f.status || undefined,
      page: this.store.page(),
      pageSize: this.store.pageSize,
    };
  });

  patch = this.store.patch;
  setPage = this.store.setPage;
  reset = this.store.reset;
}
