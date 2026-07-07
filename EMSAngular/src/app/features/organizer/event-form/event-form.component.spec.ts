import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { EventFormComponent } from './event-form.component';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { SeatService } from '../../../core/services/seat.service';
import { ScreeningService } from '../../../core/services/screening.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { ToastService } from '../../../core/services/toast.service';

describe('EventFormComponent (create mode)', () => {
  let fixture: ComponentFixture<EventFormComponent>;
  let component: EventFormComponent;
  let eventService: { create: ReturnType<typeof vi.fn> };
  let seatService: { getByVenue: ReturnType<typeof vi.fn> };
  let screeningService: { getByEvent: ReturnType<typeof vi.fn> };
  let ticketTypeService: { create: ReturnType<typeof vi.fn> };

  const seats = [
    { id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' },
    { id: 2, venueId: 1, section: 'B', row: '1', seatNumber: 1, seatType: 'General' },
  ];

  function fillEvent(): void {
    component.form.patchValue({
      venueId: 1, title: 'My Event', description: 'A great event',
      startTime: '2999-07-01T19:00', endTime: '2999-07-01T22:00',
      imageUrl: 'https://img/x.jpg', category: 'Music', screen: 'Screen 1',
    });
  }

  beforeEach(async () => {
    eventService = { create: vi.fn().mockReturnValue(of({ id: 9 })) };
    seatService = { getByVenue: vi.fn().mockReturnValue(of(seats)) };
    screeningService = { getByEvent: vi.fn().mockReturnValue(of([{ id: 5, eventId: 9, screen: '', startTime: '', endTime: '', status: 'Scheduled' }])) };
    ticketTypeService = { create: vi.fn().mockReturnValue(of({ id: 1 })) };

    await TestBed.configureTestingModule({
      imports: [EventFormComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: EventService, useValue: eventService },
        { provide: VenueService, useValue: { list: () => of([{ id: 1, name: 'Hall', address: '', city: 'X', totalCapacity: 10, layoutConfig: '', createdAt: '' }]) } },
        { provide: SeatService, useValue: seatService },
        { provide: ScreeningService, useValue: screeningService },
        { provide: TicketTypeService, useValue: ticketTypeService },
        { provide: ToastService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(EventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads venues and is in create mode', () => {
    expect(component['isEdit']()).toBe(false);
    expect(component['venues']().length).toBe(1);
  });

  it('starts with one blank ticket category', () => {
    expect(component['categories'].length).toBe(1);
  });

  it('shows the 48-hour lead-time disclaimer', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('at least 2 days (48 hours) in advance');
  });

  it('marks the start time invalid when it is within 48 hours', () => {
    const soon = new Date(Date.now() + 24 * 60 * 60_000).toISOString().slice(0, 16);
    component.form.controls.startTime.setValue(soon);
    expect(component.form.controls.startTime.hasError('minLeadTime')).toBe(true);
  });

  it('accepts a start time at least 48 hours out', () => {
    const later = new Date(Date.now() + 72 * 60 * 60_000).toISOString().slice(0, 16);
    component.form.controls.startTime.setValue(later);
    expect(component.form.controls.startTime.hasError('minLeadTime')).toBe(false);
  });

  it('loads distinct seat types when a venue is chosen', () => {
    component['onVenueChange'](1);
    expect(seatService.getByVenue).toHaveBeenCalledWith(1);
    expect(component['seatTypes']()).toEqual(['General', 'VIP']);
  });

  it('does not submit when the form is invalid', () => {
    component.submit();
    expect(eventService.create).not.toHaveBeenCalled();
  });

  it('does not submit when no ticket category has been added', () => {
    fillEvent();
    component['removeCategory'](0);
    component.submit();
    expect(eventService.create).not.toHaveBeenCalled();
  });

  it('reports the seat type capacity read-only, without a quantity control', () => {
    component['onVenueChange'](1);
    expect(component['capacityFor']('VIP')).toBe(1);
    expect(component['categories'].at(0).get('totalQuantity')).toBeNull();
  });

  it('creates the event, then a ticket type on the default screening, then navigates', () => {
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate');
    fillEvent();
    component['categories'].at(0).setValue({ name: 'VIP', seatType: 'VIP', price: 500 });

    component.submit();

    expect(eventService.create).toHaveBeenCalled();
    expect(screeningService.getByEvent).toHaveBeenCalledWith(9);
    expect(ticketTypeService.create).toHaveBeenCalledWith(
      expect.objectContaining({ screeningId: 5, name: 'VIP', seatType: 'VIP', price: 500, saleEnd: '2999-07-01T19:00' }),
    );
    expect(ticketTypeService.create.mock.calls[0][0]).not.toHaveProperty('totalQuantity');
    expect(nav).toHaveBeenCalledWith(['/organizer/events']);
  });

  it('does not submit when two categories use the same seat type', () => {
    fillEvent();
    component['onVenueChange'](1);
    component['categories'].at(0).setValue({ name: 'VIP A', seatType: 'VIP', price: 500 });
    component['addCategory']();
    component['categories'].at(1).setValue({ name: 'VIP B', seatType: 'VIP', price: 600 });

    component.submit();

    expect(eventService.create).not.toHaveBeenCalled();
  });
});
