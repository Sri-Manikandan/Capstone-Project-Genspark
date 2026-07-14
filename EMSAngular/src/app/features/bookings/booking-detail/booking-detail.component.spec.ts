import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { BookingDetailComponent } from './booking-detail.component';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto, BookingStatus } from '../../../core/models/booking.model';

function makeBooking(status: BookingStatus): BookingDto {
  return {
    id: 1, userId: 1, screeningId: 1, eventId: 5, eventTitle: 'Show', screen: 'S1',
    screeningStartTime: '2026-06-01T18:00', bookingReference: 'BK1',
    qrCode: status === 'Confirmed' || status === 'Attended' ? 'QRDATA' : '',
    totalAmount: 200, bookingStatus: status, expiresAt: '2026-06-01T18:10',
    createdAt: '2026-06-01T10:00', items: [],
  };
}

async function setup(status: BookingStatus): Promise<ComponentFixture<BookingDetailComponent>> {
  await TestBed.configureTestingModule({
    imports: [BookingDetailComponent],
    providers: [
      provideRouter([]),
      { provide: BookingService, useValue: { getById: () => of(makeBooking(status)) } },
    ],
  }).compileComponents();
  const fixture = TestBed.createComponent(BookingDetailComponent);
  fixture.detectChanges();
  return fixture;
}

describe('BookingDetailComponent', () => {
  it('shows the QR ticket for a confirmed booking', async () => {
    const fixture = await setup('Confirmed');
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ems-booking-qr')).toBeTruthy();
    expect(html.textContent).not.toContain('Complete payment');
    expect(html.textContent).toContain('Total paid');
  });

  it('hides the QR and offers Complete payment for a pending booking', async () => {
    const fixture = await setup('Pending');
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ems-booking-qr')).toBeNull();
    const pay = html.querySelector('a[href="/checkout/1"]');
    expect(pay).toBeTruthy();
    expect(html.textContent).toContain('Amount due');
  });

  it('shows neither QR nor payment action for a cancelled booking', async () => {
    const fixture = await setup('Cancelled');
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ems-booking-qr')).toBeNull();
    expect(html.textContent).not.toContain('Complete payment');
  });
});
