import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { SeatMapComponent } from './seat-map.component';
import { SeatService } from '../../../core/services/seat.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';
import { SeatDto } from '../../../core/models/seat.model';

const seats: SeatDto[] = [
  { id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' },
  { id: 2, venueId: 1, section: 'A', row: '1', seatNumber: 2, seatType: 'VIP' },
];

describe('SeatMapComponent', () => {
  let fixture: ComponentFixture<SeatMapComponent>;
  let component: SeatMapComponent;
  let hub: { lastUpdate: ReturnType<typeof signal<any>>; joinEvent: ReturnType<typeof vi.fn>; leaveEvent: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    hub = {
      lastUpdate: signal(null),
      joinEvent: vi.fn().mockResolvedValue(undefined),
      leaveEvent: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({
      imports: [SeatMapComponent],
      providers: [
        { provide: SeatService, useValue: { getAvailableByEvent: () => of(seats) } },
        { provide: SeatHubService, useValue: hub },
      ],
    });
    fixture = TestBed.createComponent(SeatMapComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('eventId', 10);
    fixture.componentRef.setInput('venueId', 1);
    fixture.detectChanges();
  });

  it('marks fetched seats available and joins the event room', () => {
    expect((component as any)['seatState'](seats[0])).toBe('available');
    expect(hub.joinEvent).toHaveBeenCalledWith(10);
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
