import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { EventApprovalsComponent } from './event-approvals.component';
import { AdminService } from '../../../core/services/admin.service';

// The backend returns a plain array of enriched pending events (List<PendingEventReviewDto>).
const pending = [{
  id: 3, title: 'Pending Show', description: 'A wonderful evening of live music and more.',
  category: 'Music', imageUrl: 'https://img/x.jpg',
  startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', screen: 'Screen 1',
  createdAt: '2026-06-01T10:00:00', rejectionReason: null,
  venueId: 1, venueName: 'Grand Hall', city: 'Chennai',
  organizer: {
    id: 1, name: 'Asha Rao', email: 'asha@example.com', phone: '999', memberSince: '2025-01-01T00:00:00',
    isActive: true, publishedEventCount: 3, rejectedEventCount: 1, totalEventCount: 5,
  },
  screens: [
    {
      screen: 'Screen 1',
      showtimes: [
        { startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00' },
        { startTime: '2026-07-02T19:00:00', endTime: '2026-07-02T22:00:00' },
      ],
      ticketCategories: [
        { name: 'Gold', seatType: 'Premium', price: 500, totalQuantity: 100 },
        { name: 'Silver', seatType: 'Standard', price: 300, totalQuantity: 200 },
      ],
    },
    {
      screen: 'Screen 2',
      showtimes: [{ startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00' }],
      ticketCategories: [{ name: 'Gold', seatType: 'Premium', price: 500, totalQuantity: 80 }],
    },
  ],
  signals: { leadTimeOk: true, imageUrlValid: true, descriptionAdequate: true, hasTicketCategories: true, pricingSane: true },
}];

describe('EventApprovalsComponent', () => {
  let fixture: ComponentFixture<EventApprovalsComponent>;
  let component: EventApprovalsComponent;
  let admin: { getPendingEvents: ReturnType<typeof vi.fn>; approveEvent: ReturnType<typeof vi.fn>; rejectEvent: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    admin = {
      getPendingEvents: vi.fn().mockReturnValue(of(pending)),
      approveEvent: vi.fn().mockReturnValue(of({})),
      rejectEvent: vi.fn().mockReturnValue(of({})),
    };
    TestBed.configureTestingModule({
      imports: [EventApprovalsComponent],
      providers: [{ provide: AdminService, useValue: admin }],
    });
    fixture = TestBed.createComponent(EventApprovalsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads pending events', () => {
    expect(component['events']().length).toBe(1);
  });

  it('renders organizer profile and track record', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Asha Rao');
    expect(text).toContain('asha@example.com');
    expect(text).toContain('3 published');
    expect(text).toContain('5 total');
  });

  it('renders each screen with its showtimes and per-screen categories', () => {
    const text = fixture.nativeElement.textContent as string;
    // Both screens are shown as distinct groups.
    expect(text).toContain('Screen 1');
    expect(text).toContain('Screen 2');
    // A screen count summarises the multi-screen event.
    expect(text).toContain('2 screens');
    // Per-screen capacity is preserved (Screen 2's Gold has 80, not merged with Screen 1's 100).
    expect(text).toContain('80');
    expect(text).toContain('Silver');
    expect(text).toContain('Grand Hall');
    expect(text).toContain('Review checks');
  });

  it('renders the empty state without crashing when none are pending', () => {
    admin.getPendingEvents.mockReturnValue(of([]));
    component['ngOnInit']();
    fixture.detectChanges();
    expect(component['events']()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('No pending events.');
  });

  it('approves an event then reloads', () => {
    admin.getPendingEvents.mockClear();
    component['approve'](3);
    expect(admin.approveEvent).toHaveBeenCalledWith(3, {});
    expect(admin.getPendingEvents).toHaveBeenCalled();
  });

  it('rejects with the entered reason', () => {
    component['reasons'].set({ 3: 'Inappropriate content' });
    component['reject'](3);
    expect(admin.rejectEvent).toHaveBeenCalledWith(3, { reason: 'Inappropriate content' });
  });
});
