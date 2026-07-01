import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { EventService } from './event.service';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import { EventDto } from '../models/event.model';

describe('EventService', () => {
  let service: EventService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/v1/Event`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), EventService],
    });
    service = TestBed.inject(EventService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('search builds query params and returns paged result', () => {
    const paged: PagedResult<EventDto> = { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 };
    service.search({ query: 'rock', page: 1, pageSize: 10 }).subscribe((r: PagedResult<EventDto>) => expect(r).toEqual(paged));
    const req = http.expectOne(r => r.url === base);
    expect(req.request.params.get('query')).toBe('rock');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(paged);
  });

  it('search sends the city filter as a query param', () => {
    service.search({ city: 'Chennai', page: 1, pageSize: 10 }).subscribe();
    const req = http.expectOne(r => r.url === base);
    expect(req.request.params.get('city')).toBe('Chennai');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 });
  });

  it('getCities hits cities endpoint', () => {
    service.getCities().subscribe(cities => expect(cities).toEqual(['Chennai', 'Madurai']));
    const req = http.expectOne(`${base}/cities`);
    expect(req.request.method).toBe('GET');
    req.flush(['Chennai', 'Madurai']);
  });

  it('getBySlug hits slug endpoint', () => {
    service.getBySlug('my-event').subscribe();
    http.expectOne(`${base}/slug/my-event`).flush({} as EventDto);
  });

  it('submit posts to submit endpoint', () => {
    service.submit(7).subscribe();
    const req = http.expectOne(`${base}/7/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({} as EventDto);
  });

  it('maps error to string message', () => {
    service.getById(99).subscribe({ error: (e: string) => expect(e).toBe('Event not found.') });
    http.expectOne(`${base}/99`).flush({ message: 'Event not found.' }, { status: 404, statusText: 'Not Found' });
  });
});
