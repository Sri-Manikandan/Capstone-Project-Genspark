import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminUsersComponent } from './admin-users.component';
import { AdminService } from '../../../core/services/admin.service';
import { UserFilterStore } from './user-filter.store';

const paged = {
  items: [{ id: 1, name: 'Ada', email: 'ada@x.io', role: 'User', isActive: true }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('AdminUsersComponent', () => {
  let component: AdminUsersComponent;
  let fixture: ComponentFixture<AdminUsersComponent>;
  let store: UserFilterStore;
  let getUsers: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    getUsers = vi.fn().mockReturnValue(of(paged));
    TestBed.configureTestingModule({
      imports: [AdminUsersComponent],
      providers: [{ provide: AdminService, useValue: { getUsers, deleteUser: () => of(void 0) } }],
    });
    store = TestBed.inject(UserFilterStore);
    store.reset();
    fixture = TestBed.createComponent(AdminUsersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads users on init', () => {
    expect(getUsers).toHaveBeenCalled();
    expect(component['users']().length).toBe(1);
  });

  it('maps active filter to isActive in the request', () => {
    getUsers.mockClear();
    store.patch({ active: 'inactive' });
    fixture.detectChanges();
    expect(getUsers).toHaveBeenCalledWith(expect.objectContaining({ isActive: false }));
  });
});
