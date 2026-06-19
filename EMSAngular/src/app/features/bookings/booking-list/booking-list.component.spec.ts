import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BookingListComponent } from './booking-list.component';
import { BookingService } from '../../../core/services/booking.service';

const paged = {
  items: [{ id: 1, userId: 1, eventId: 5, eventTitle: 'Show', bookingReference: 'BK1', qrCode: '',
    totalAmount: 200, bookingStatus: 'Confirmed', expiresAt: '', createdAt: '2026-06-01T10:00:00', items: [] }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('BookingListComponent', () => {
  let fixture: ComponentFixture<BookingListComponent>;
  let component: BookingListComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BookingListComponent],
      providers: [provideRouter([]), { provide: BookingService, useValue: { getMyBookings: () => of(paged) } }],
    });
    fixture = TestBed.createComponent(BookingListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads my bookings on init', () => {
    expect(component['bookings']().length).toBe(1);
    expect(component['loading']()).toBe(false);
  });
});
