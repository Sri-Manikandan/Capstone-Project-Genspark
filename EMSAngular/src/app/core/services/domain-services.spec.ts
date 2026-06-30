import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { BookingService } from './booking.service';
import { PaymentService } from './payment.service';
import { SeatService } from './seat.service';
import { TicketTypeService } from './ticket-type.service';
import { VenueService } from './venue.service';
import { UserService } from './user.service';
import { AdminService } from './admin.service';

describe('domain services', () => {
  let http: HttpTestingController;
  const root = `${environment.apiBaseUrl}/api/v1`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        BookingService,
        PaymentService,
        SeatService,
        TicketTypeService,
        VenueService,
        UserService,
        AdminService,
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('BookingService.create posts to /Booking with an idempotency key', () => {
    TestBed.inject(BookingService).create({ eventId: 1, items: [] }, 'key-123').subscribe();
    const req = http.expectOne(`${root}/Booking`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Idempotency-Key')).toBe('key-123');
    req.flush({});
  });

  it('PaymentService.initiate posts to /Payment/initiate with an idempotency key', () => {
    TestBed.inject(PaymentService).initiate({ bookingId: 1, currency: 'inr' }).subscribe();
    const req = http.expectOne(`${root}/Payment/initiate`);
    expect(req.request.headers.get('Idempotency-Key')).toBe('payment-initiate-1');
    req.flush({});
  });

  it('SeatService.reserve posts to /Seat/reserve', () => {
    TestBed.inject(SeatService).reserve({ eventId: 1, seatId: 2, ticketTypeId: 3 }).subscribe();
    http.expectOne(`${root}/Seat/reserve`).flush({});
  });

  it('TicketTypeService.getActiveByEvent hits active endpoint', () => {
    TestBed.inject(TicketTypeService).getActiveByEvent(5).subscribe();
    http.expectOne(`${root}/TicketType/event/5/active`).flush([]);
  });

  it('VenueService.list gets /Venue', () => {
    TestBed.inject(VenueService).list().subscribe();
    http.expectOne(`${root}/Venue`).flush([]);
  });

  it('UserService.getMe gets /User/me', () => {
    TestBed.inject(UserService).getMe().subscribe();
    http.expectOne(`${root}/User/me`).flush({});
  });

  it('AdminService.getPendingEvents gets /Admin/events/pending', () => {
    TestBed.inject(AdminService).getPendingEvents(1, 10).subscribe();
    const req = http.expectOne(r => r.url === `${root}/Admin/events/pending`);
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 });
  });
});
