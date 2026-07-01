import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { EventDetailComponent } from './event-detail.component';
import { EventService } from '../../../core/services/event.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { SeatService } from '../../../core/services/seat.service';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';

const ev = {
  id: 5, organizerId: 1, venueId: 2, title: 'Show', description: 'd', status: 'Published',
  startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', imageUrl: '', category: 'Music',
  slug: 'show', createdAt: '2026-06-01T00:00:00',
};
const tt = {
  id: 9, eventId: 5, name: 'VIP', seatType: 'VIP', price: 100, totalQuantity: 50,
  availableQuantity: 50, saleStart: '2026-06-01T00:00:00', saleEnd: '2026-06-30T00:00:00',
  isActive: true, createdAt: '',
};
const reservation = {
  id: 77, seatId: 1, eventId: 5, ticketTypeId: 9, userId: 1,
  status: 'Active', reservedUntil: '', createdAt: '',
};

describe('EventDetailComponent', () => {
  let fixture: ComponentFixture<EventDetailComponent>;
  let component: EventDetailComponent;
  let bookingCreate: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    bookingCreate = vi.fn().mockReturnValue(of({ id: 123 }));

    await TestBed.configureTestingModule({
      imports: [EventDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'show' } } } },
        { provide: EventService, useValue: { getBySlug: () => of(ev) } },
        { provide: TicketTypeService, useValue: { getActiveByEvent: () => of([tt]) } },
        {
          provide: SeatService,
          useValue: {
            reserve: () => of(reservation),
            releaseReservation: () => of(void 0),
          },
        },
        { provide: BookingService, useValue: { create: bookingCreate } },
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } },
        {
          provide: SeatHubService,
          useValue: {
            lastUpdate: signal(null),
            joinEvent: () => Promise.resolve(),
            leaveEvent: () => Promise.resolve(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EventDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the event and ticket types', () => {
    expect(component['event']()?.title).toBe('Show');
    expect(component['ticketTypes']().length).toBe(1);
  });

  it('picker confirm sets the category, quantity and opens the seats step', () => {
    component['onPickerConfirm']({ ticketType: tt, quantity: 3 });
    expect(component['selectedTicketType']()?.id).toBe(9);
    expect(component['quantity']()).toBe(3);
    expect(component['step']()).toBe('seats');
    expect(component['showPicker']()).toBe(false);
    expect(component['restrictToSeatType']()).toBe('VIP');
  });

  it('reserves a seat against the chosen category and tracks it in selected', () => {
    component['onPickerConfirm']({ ticketType: tt, quantity: 2 });
    component['onSeatToggled']({ id: 1, venueId: 2, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' });
    expect(component['selected']().length).toBe(1);
    expect(component['selected']()[0].reservation.id).toBe(77);
    expect(component['selected']()[0].ticketType.id).toBe(9);
  });

  it('ignores seat toggles until a category is chosen', () => {
    component['onSeatToggled']({ id: 1, venueId: 2, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' });
    expect(component['selected']().length).toBe(0);
  });

  it('checkout creates a booking and navigates', () => {
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component['onPickerConfirm']({ ticketType: tt, quantity: 1 });
    component['onSeatToggled']({ id: 1, venueId: 2, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' });
    component['checkout']();
    expect(bookingCreate).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/checkout', 123]);
  });
});
