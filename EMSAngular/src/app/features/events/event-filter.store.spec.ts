import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { EventFilterStore } from './event-filter.store';
import { EventService } from '../../core/services/event.service';

describe('EventFilterStore', () => {
  let store: EventFilterStore;
  let getCategories: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getCategories = vi.fn().mockReturnValue(of(['Music', 'Tech']));
    TestBed.configureTestingModule({
      providers: [{ provide: EventService, useValue: { getCategories } }],
    });
    store = TestBed.inject(EventFilterStore);
    store.reset();
  });

  it('omits empty filters from the request', () => {
    const req = store.request();
    expect(req.query).toBeUndefined();
    expect(req.category).toBeUndefined();
    expect(req.startFrom).toBeUndefined();
    expect(req.sortBy).toBe('startTime');
    expect(req.page).toBe(1);
    expect(req.pageSize).toBe(9);
  });

  it('includes set filters and resets page on patch', () => {
    store.setPage(3);
    store.patch({ query: 'jazz', category: 'Music' });
    const req = store.request();
    expect(req.query).toBe('jazz');
    expect(req.category).toBe('Music');
    expect(req.page).toBe(1);
  });

  it('loads categories once', () => {
    store.loadCategories();
    store.loadCategories();
    expect(getCategories).toHaveBeenCalledTimes(1);
    expect(store.categories()).toEqual(['Music', 'Tech']);
  });
});
