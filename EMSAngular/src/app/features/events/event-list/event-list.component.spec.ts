import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EventListComponent } from './event-list.component';
import { EventService } from '../../../core/services/event.service';
import { PagedResult } from '../../../core/models/paged-result.model';
import { EventDto } from '../../../core/models/event.model';

const page: PagedResult<EventDto> = {
  items: [{ id: 1, organizerId: 1, venueId: 1, title: 'Jazz', description: '', status: 'Published',
    startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', imageUrl: '', category: 'Music',
    slug: 'jazz', createdAt: '2026-06-01T00:00:00' }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('EventListComponent', () => {
  let fixture: ComponentFixture<EventListComponent>;
  let component: EventListComponent;
  let search: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    search = vi.fn().mockReturnValue(of(page));
    TestBed.configureTestingModule({
      imports: [EventListComponent],
      providers: [provideRouter([]), { provide: EventService, useValue: { search } }],
    });
    fixture = TestBed.createComponent(EventListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads events on init', () => {
    expect(search).toHaveBeenCalled();
    expect(component['events']().length).toBe(1);
    expect(component['loading']()).toBe(false);
  });

  it('re-searches when applyFilters is called', () => {
    search.mockClear();
    component['applyFilters']();
    expect(search).toHaveBeenCalled();
  });
});
