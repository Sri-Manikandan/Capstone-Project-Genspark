import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { EventFormComponent } from './event-form.component';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { SeatService } from '../../../core/services/seat.service';
import { ToastService } from '../../../core/services/toast.service';

describe('EventFormComponent (create mode)', () => {
  let fixture: ComponentFixture<EventFormComponent>;
  let component: EventFormComponent;
  let eventService: { createWithScreenings: ReturnType<typeof vi.fn> };
  let seatService: { getByVenue: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  // Screen A: VIP + Normal. Screen B: Premium + Normal. The only type common to both is Normal.
  const seats = [
    { id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' },
    { id: 2, venueId: 1, section: 'A', row: '1', seatNumber: 2, seatType: 'Normal' },
    { id: 3, venueId: 1, section: 'B', row: '1', seatNumber: 1, seatType: 'Premium' },
    { id: 4, venueId: 1, section: 'B', row: '1', seatNumber: 2, seatType: 'Normal' },
    { id: 5, venueId: 1, section: 'B', row: '1', seatNumber: 3, seatType: 'Normal' },
  ];

  function fillBaseFields(): void {
    component.form.patchValue({
      venueId: 1, title: 'My Event', description: 'A great event',
      imageUrl: 'https://img/x.jpg', category: 'Music',
    });
  }

  function setShowtime(index: number, screen: string, start: string, end: string): void {
    component['showtimes'].at(index).setValue({ screen, startTime: start, endTime: end });
    component['onShowtimeScreenChange']();
  }

  beforeEach(async () => {
    eventService = { createWithScreenings: vi.fn().mockReturnValue(of({ id: 9 })) };
    seatService = { getByVenue: vi.fn().mockReturnValue(of(seats)) };
    toast = { success: vi.fn(), error: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [EventFormComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: EventService, useValue: eventService },
        { provide: VenueService, useValue: { list: () => of([{ id: 1, name: 'Hall', address: '', city: 'X', totalCapacity: 10, layoutConfig: '', createdAt: '' }]) } },
        { provide: SeatService, useValue: seatService },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(EventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('is in create mode and starts with one showtime and one category', () => {
    expect(component['isEdit']()).toBe(false);
    expect(component['showtimes'].length).toBe(1);
    expect(component['categories'].length).toBe(1);
  });

  it('loads the venue screens when a venue is chosen', () => {
    component['onVenueChange'](1);
    expect(seatService.getByVenue).toHaveBeenCalledWith(1);
    expect(component['screens']()).toEqual(['A', 'B']);
  });

  it('offers only seat types shared by every selected screen', () => {
    component['onVenueChange'](1);
    // One screen A → both of A's types.
    setShowtime(0, 'A', '2999-07-01T18:00', '2999-07-01T21:00');
    expect(component['seatTypes']()).toEqual(['Normal', 'VIP']);

    // Add screen B → intersection is just Normal.
    component['addShowtime']();
    setShowtime(1, 'B', '2999-07-02T18:00', '2999-07-02T21:00');
    expect(component['seatTypes']()).toEqual(['Normal']);
    expect(component['totalCapacityFor']('Normal')).toBe(3); // 1 on A + 2 on B
  });

  it('creates the event with one showtime per screening and shared categories', () => {
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate');
    fillBaseFields();
    component['onVenueChange'](1);
    setShowtime(0, 'A', '2999-07-01T18:00', '2999-07-01T21:00');
    component['addShowtime']();
    setShowtime(1, 'B', '2999-07-02T18:00', '2999-07-02T21:00');
    component['categories'].at(0).setValue({ name: 'Standard', seatType: 'Normal', price: 200 });

    component.submit();

    expect(eventService.createWithScreenings).toHaveBeenCalledWith(
      expect.objectContaining({
        venueId: 1, title: 'My Event', category: 'Music',
        showtimes: [
          { screen: 'A', startTime: '2999-07-01T18:00', endTime: '2999-07-01T21:00' },
          { screen: 'B', startTime: '2999-07-02T18:00', endTime: '2999-07-02T21:00' },
        ],
        ticketCategories: [{ name: 'Standard', seatType: 'Normal', price: 200 }],
      }),
    );
    expect(nav).toHaveBeenCalledWith(['/organizer/events']);
  });

  it('blocks submission when a screen has overlapping showtimes', () => {
    fillBaseFields();
    component['onVenueChange'](1);
    setShowtime(0, 'A', '2999-07-01T18:00', '2999-07-01T21:00');
    component['addShowtime']();
    setShowtime(1, 'A', '2999-07-01T20:00', '2999-07-01T22:00'); // overlaps the first on A
    component['categories'].at(0).setValue({ name: 'VIP', seatType: 'VIP', price: 500 });

    component.submit();

    expect(eventService.createWithScreenings).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith(expect.stringContaining('overlapping'));
  });

  it('blocks submission when two categories share a seat type', () => {
    fillBaseFields();
    component['onVenueChange'](1);
    setShowtime(0, 'A', '2999-07-01T18:00', '2999-07-01T21:00');
    component['categories'].at(0).setValue({ name: 'A', seatType: 'VIP', price: 500 });
    component['addCategory']();
    component['categories'].at(1).setValue({ name: 'B', seatType: 'VIP', price: 600 });

    component.submit();

    expect(eventService.createWithScreenings).not.toHaveBeenCalled();
  });
});
