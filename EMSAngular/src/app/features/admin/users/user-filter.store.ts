import { Injectable, computed } from '@angular/core';
import { Role, UserSearchRequest } from '../../../core/models/user.model';
import { createFilterStore } from '../../../core/state/filter-store';

export interface UserFilters {
  query: string;
  role: Role | '';
  active: 'all' | 'active' | 'inactive';
}

const INITIAL: UserFilters = { query: '', role: '', active: 'all' };

@Injectable({ providedIn: 'root' })
export class UserFilterStore {
  private store = createFilterStore<UserFilters>({ initial: INITIAL, pageSize: 10 });

  readonly filters = this.store.filters;
  readonly page = this.store.page;

  readonly request = computed<UserSearchRequest>(() => {
    const f = this.store.filters();
    return {
      query: f.query || undefined,
      role: f.role || undefined,
      isActive: f.active === 'all' ? undefined : f.active === 'active',
      page: this.store.page(),
      pageSize: this.store.pageSize,
    };
  });

  patch = this.store.patch;
  setPage = this.store.setPage;
  reset = this.store.reset;
}
