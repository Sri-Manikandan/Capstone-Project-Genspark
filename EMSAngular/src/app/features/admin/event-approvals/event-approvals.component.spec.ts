import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { EventApprovalsComponent } from './event-approvals.component';
import { AdminService } from '../../../core/services/admin.service';

const paged = {
  items: [{ id: 3, organizerId: 1, venueId: 1, title: 'Pending Show', description: '', status: 'PendingApproval' as const,
    startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', imageUrl: '', category: 'Music',
    slug: 'pending-show', createdAt: '' }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('EventApprovalsComponent', () => {
  let fixture: ComponentFixture<EventApprovalsComponent>;
  let component: EventApprovalsComponent;
  let admin: { getPendingEvents: ReturnType<typeof vi.fn>; approveEvent: ReturnType<typeof vi.fn>; rejectEvent: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    admin = {
      getPendingEvents: vi.fn().mockReturnValue(of(paged)),
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
