import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BookingListComponent } from './booking-list.component';
import { BookingService } from '../../../core/services/booking.service';
import { BookingFilterStore } from '../booking-filter.store';

const paged = {
  items: [
    { id: 1, userId: 1, eventId: 5, eventTitle: 'Show', bookingReference: 'BK1', qrCode: '',
      totalAmount: 200, bookingStatus: 'Confirmed', expiresAt: '', createdAt: '2026-06-01T10:00:00', items: [] },
    { id: 2, userId: 1, eventId: 5, eventTitle: 'Gig', bookingReference: 'BK2', qrCode: '',
      totalAmount: 150, bookingStatus: 'Pending', expiresAt: '', createdAt: '2026-06-02T10:00:00', items: [] },
  ],
  totalCount: 2, page: 1, pageSize: 10, totalPages: 1,
};

describe('BookingListComponent', () => {
  let fixture: ComponentFixture<BookingListComponent>;
  let component: BookingListComponent;
  let store: BookingFilterStore;
  let getMyBookings: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getMyBookings = vi.fn().mockReturnValue(of(paged));
    TestBed.configureTestingModule({
      imports: [BookingListComponent],
      providers: [provideRouter([]), { provide: BookingService, useValue: { getMyBookings } }],
    });
    store = TestBed.inject(BookingFilterStore);
    store.reset();
    fixture = TestBed.createComponent(BookingListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads my bookings on init', () => {
    expect(component['bookings']().length).toBe(2);
    expect(component['loading']()).toBe(false);
  });

  it('offers Complete payment only for the pending booking', () => {
    const html = fixture.nativeElement as HTMLElement;
    const payLinks = html.querySelectorAll('a[href="/checkout/2"]');
    expect(payLinks.length).toBe(1);
    // The confirmed booking gets no checkout link.
    expect(html.querySelector('a[href="/checkout/1"]')).toBeNull();
  });

  it('re-loads when the status filter changes', () => {
    getMyBookings.mockClear();
    store.patch({ status: 'Confirmed' });
    fixture.detectChanges();
    expect(getMyBookings).toHaveBeenCalledWith(expect.objectContaining({ status: 'Confirmed' }));
  });
});
