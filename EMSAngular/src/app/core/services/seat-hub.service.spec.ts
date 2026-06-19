import { TestBed } from '@angular/core/testing';
import { SeatHubService } from './seat-hub.service';

describe('SeatHubService', () => {
  let service: SeatHubService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeatHubService] });
    service = TestBed.inject(SeatHubService);
  });

  it('starts with no update', () => {
    expect(service.lastUpdate()).toBeNull();
  });

  it('handleSeatEvent updates the signal', () => {
    service.handleSeatEvent('reserved', 42);
    expect(service.lastUpdate()).toEqual({ seatId: 42, status: 'reserved' });
    service.handleSeatEvent('booked', 7);
    expect(service.lastUpdate()).toEqual({ seatId: 7, status: 'booked' });
  });
});
