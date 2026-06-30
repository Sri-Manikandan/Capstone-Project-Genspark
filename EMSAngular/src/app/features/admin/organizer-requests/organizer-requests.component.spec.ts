import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { OrganizerRequestsComponent } from './organizer-requests.component';
import { AdminService } from '../../../core/services/admin.service';
import { OrganizerRequestFilterStore } from './organizer-request-filter.store';

const paged = {
  items: [{ id: 1, userId: 2, userName: 'Ada', userEmail: 'ada@x.io', status: 'Pending', requestedAt: '2026-06-01T10:00:00' }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('OrganizerRequestsComponent', () => {
  let component: OrganizerRequestsComponent;
  let fixture: ComponentFixture<OrganizerRequestsComponent>;
  let store: OrganizerRequestFilterStore;
  let getOrganizerRequests: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getOrganizerRequests = vi.fn().mockReturnValue(of(paged));
    TestBed.configureTestingModule({
      imports: [OrganizerRequestsComponent],
      providers: [{ provide: AdminService, useValue: { getOrganizerRequests } }],
    });
    store = TestBed.inject(OrganizerRequestFilterStore);
    store.reset();
    fixture = TestBed.createComponent(OrganizerRequestsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads pending requests on init', () => {
    expect(getOrganizerRequests).toHaveBeenCalledWith(expect.objectContaining({ status: 'Pending' }));
    expect(component['requests']().length).toBe(1);
  });

  it('re-loads when the status filter changes', () => {
    getOrganizerRequests.mockClear();
    store.patch({ status: 'Approved' });
    fixture.detectChanges();
    expect(getOrganizerRequests).toHaveBeenCalledWith(expect.objectContaining({ status: 'Approved' }));
  });
});
