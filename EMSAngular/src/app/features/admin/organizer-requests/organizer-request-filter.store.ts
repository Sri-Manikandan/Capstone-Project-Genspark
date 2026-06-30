import { Injectable, computed } from '@angular/core';
import { OrganizerRequestQueryRequest } from '../../../core/models/admin.model';
import { createFilterStore } from '../../../core/state/filter-store';

export interface OrganizerRequestFilters {
  status: string;
}

const INITIAL: OrganizerRequestFilters = { status: 'Pending' };

@Injectable({ providedIn: 'root' })
export class OrganizerRequestFilterStore {
  private store = createFilterStore<OrganizerRequestFilters>({ initial: INITIAL, pageSize: 10 });

  readonly filters = this.store.filters;
  readonly page = this.store.page;

  readonly request = computed<OrganizerRequestQueryRequest>(() => {
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
