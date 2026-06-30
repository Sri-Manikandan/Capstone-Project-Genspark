import { createFilterStore } from './filter-store';

interface Sample {
  query: string;
  status: string;
}

describe('createFilterStore', () => {
  const make = () =>
    createFilterStore<Sample>({ initial: { query: '', status: '' }, pageSize: 9 });

  it('starts with the initial filters and page 1', () => {
    const store = make();
    expect(store.filters()).toEqual({ query: '', status: '' });
    expect(store.page()).toBe(1);
    expect(store.pageSize).toBe(9);
  });

  it('patch merges filters and resets page to 1', () => {
    const store = make();
    store.setPage(3);
    store.patch({ query: 'jazz' });
    expect(store.filters()).toEqual({ query: 'jazz', status: '' });
    expect(store.page()).toBe(1);
  });

  it('setPage changes page without touching filters', () => {
    const store = make();
    store.patch({ query: 'jazz' });
    store.setPage(4);
    expect(store.page()).toBe(4);
    expect(store.filters()).toEqual({ query: 'jazz', status: '' });
  });

  it('reset restores initial filters and page 1', () => {
    const store = make();
    store.patch({ query: 'jazz', status: 'Published' });
    store.setPage(5);
    store.reset();
    expect(store.filters()).toEqual({ query: '', status: '' });
    expect(store.page()).toBe(1);
  });
});
