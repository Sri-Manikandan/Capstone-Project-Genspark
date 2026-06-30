import { Signal, WritableSignal, signal } from '@angular/core';

/**
 * Generic, type-safe filter state for a server-backed list. Holds the active
 * filters and the current page; changing any filter resets to page 1.
 */
export interface FilterStore<T extends object> {
  readonly filters: Signal<T>;
  readonly page: WritableSignal<number>;
  readonly pageSize: number;
  /** Merge a partial set of filters and return to the first page. */
  patch(partial: Partial<T>): void;
  /** Change the page without touching filters (pagination). */
  setPage(p: number): void;
  /** Restore the initial filters and reset to page 1. */
  reset(): void;
}

export function createFilterStore<T extends object>(config: {
  initial: T;
  pageSize: number;
}): FilterStore<T> {
  const filters = signal<T>({ ...config.initial });
  const page = signal(1);

  return {
    filters,
    page,
    pageSize: config.pageSize,
    patch(partial: Partial<T>): void {
      filters.update(current => ({ ...current, ...partial }));
      page.set(1);
    },
    setPage(p: number): void {
      page.set(p);
    },
    reset(): void {
      filters.set({ ...config.initial });
      page.set(1);
    },
  };
}
