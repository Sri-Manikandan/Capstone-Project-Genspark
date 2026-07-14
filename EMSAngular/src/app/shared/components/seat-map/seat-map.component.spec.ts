import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { SeatMapComponent } from './seat-map.component';
import { SeatService } from '../../../core/services/seat.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';
import { SeatDto } from '../../../core/models/seat.model';

const seats: SeatDto[] = [
  { id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP', isAvailable: true },
  { id: 2, venueId: 1, section: 'A', row: '1', seatNumber: 2, seatType: 'VIP', isAvailable: true },
];

describe('SeatMapComponent', () => {
  let fixture: ComponentFixture<SeatMapComponent>;
  let component: SeatMapComponent;
  let hub: { lastUpdate: ReturnType<typeof signal<any>>; joinScreening: ReturnType<typeof vi.fn>; leaveScreening: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    hub = {
      lastUpdate: signal(null),
      joinScreening: vi.fn().mockResolvedValue(undefined),
      leaveScreening: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({
      imports: [SeatMapComponent],
      providers: [
        { provide: SeatService, useValue: { getAvailableByScreening: () => of(seats) } },
        { provide: SeatHubService, useValue: hub },
      ],
    });
    fixture = TestBed.createComponent(SeatMapComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('screeningId', 10);
    fixture.componentRef.setInput('venueId', 1);
    fixture.detectChanges();
  });

  it('marks fetched seats available and joins the screening room', () => {
    expect((component as any)['seatState'](seats[0])).toBe('available');
    expect(hub.joinScreening).toHaveBeenCalledWith(10);
  });

  it('emits when an available seat is clicked', () => {
    let emitted: SeatDto | null = null;
    component.seatToggled.subscribe((s: SeatDto) => (emitted = s));
    (component as any)['onSeatClick'](seats[0]);
    expect(emitted).toEqual(seats[0]);
  });

  it('marks a seat taken after a SeatBooked hub event', () => {
    hub.lastUpdate.set({ seatId: 1, status: 'booked' });
    fixture.detectChanges();
    expect((component as any)['seatState'](seats[0])).toBe('taken');
  });
});

describe('SeatMapComponent — already-booked seats', () => {
  const grid: SeatDto[] = [
    { id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP', isAvailable: false },
    { id: 2, venueId: 1, section: 'A', row: '1', seatNumber: 2, seatType: 'VIP', isAvailable: true },
  ];

  let fixture: ComponentFixture<SeatMapComponent>;
  let component: SeatMapComponent;

  beforeEach(() => {
    const hub = {
      lastUpdate: signal(null),
      joinScreening: vi.fn().mockResolvedValue(undefined),
      leaveScreening: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({
      imports: [SeatMapComponent],
      providers: [
        { provide: SeatService, useValue: { getAvailableByScreening: () => of(grid) } },
        { provide: SeatHubService, useValue: hub },
      ],
    });
    fixture = TestBed.createComponent(SeatMapComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('screeningId', 10);
    fixture.componentRef.setInput('venueId', 1);
    fixture.detectChanges();
  });

  it('renders a seat the API flags unavailable as taken, not hidden', () => {
    // Seat 1 stays in the grid (so the layout does not collapse) but is not selectable.
    expect((component as any)['seatState'](grid[0])).toBe('taken');
    expect((component as any)['seatState'](grid[1])).toBe('available');
    const sections = (component as any)['sections']();
    expect(sections[0].rows[0].seats).toHaveLength(2);
  });
});
