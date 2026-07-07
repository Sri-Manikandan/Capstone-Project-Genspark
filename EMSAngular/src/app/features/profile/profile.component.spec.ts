import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ProfileComponent } from './profile.component';
import { UserService } from '../../core/services/user.service';
import { AuthService } from '../../core/services/auth.service';
import { User } from '../../core/models/user.model';

const me: User = {
  id: 1,
  name: 'Ada Lovelace',
  email: 'ada@example.com',
  phone: '+911234567',
  role: 'User',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
};

describe('ProfileComponent', () => {
  let component: ProfileComponent;
  let fixture: ComponentFixture<ProfileComponent>;
  let userService: Record<string, ReturnType<typeof vi.fn>>;

  beforeEach(async () => {
    userService = {
      getMe: vi.fn().mockReturnValue(of(me)),
      getOrganizerRequest: vi.fn().mockReturnValue(of(null)),
      updateMe: vi.fn(),
      changePassword: vi.fn(),
      changeEmail: vi.fn(),
      deleteMe: vi.fn(),
      requestOrganizer: vi.fn(),
    };
    const auth = { setCurrentUser: vi.fn(), logout: vi.fn() };
    const router = { navigate: vi.fn(), navigateByUrl: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // Protected members are reachable via index access in tests.
  const ctrl = (form: string, name: string) => (component as any)[form].get(name);

  it('loads the current user on init', () => {
    expect(component).toBeTruthy();
    expect(userService['getMe']).toHaveBeenCalled();
  });

  it('rejects a phone that is not 7–15 digits', () => {
    ctrl('profileForm', 'name').setValue('Grace Hopper');
    ctrl('profileForm', 'phone').setValue('abc');
    (component as any).saveProfile();
    expect(userService['updateMe']).not.toHaveBeenCalled();
    expect(ctrl('profileForm', 'phone').errors?.['pattern']).toBeTruthy();
  });

  it('rejects a name shorter than 2 characters', () => {
    ctrl('profileForm', 'name').setValue('A');
    ctrl('profileForm', 'phone').setValue('+911234567');
    (component as any).saveProfile();
    expect(userService['updateMe']).not.toHaveBeenCalled();
  });

  it('rejects a new password missing complexity', () => {
    ctrl('passwordForm', 'currentPassword').setValue('old');
    ctrl('passwordForm', 'newPassword').setValue('lowercaseonly');
    ctrl('passwordForm', 'confirmPassword').setValue('lowercaseonly');
    (component as any).changePassword();
    expect(userService['changePassword']).not.toHaveBeenCalled();
    expect(ctrl('passwordForm', 'newPassword').errors?.['complexity']).toBeTruthy();
  });

  it('accepts a fully complex password', () => {
    userService['changePassword'].mockReturnValue(of(void 0));
    ctrl('passwordForm', 'currentPassword').setValue('OldPass1!');
    ctrl('passwordForm', 'newPassword').setValue('NewPass1!');
    ctrl('passwordForm', 'confirmPassword').setValue('NewPass1!');
    (component as any).changePassword();
    expect(userService['changePassword']).toHaveBeenCalled();
    expect(component['passwordSuccess']()).toBe('Password updated.');
  });

  it('sets the profile error on a failed save and clears it on retry', () => {
    ctrl('profileForm', 'name').setValue('Grace Hopper');
    ctrl('profileForm', 'phone').setValue('+911234567');

    userService['updateMe'].mockReturnValue(throwError(() => 'Server said no'));
    (component as any).saveProfile();
    expect(component['profileError']()).toBe('Server said no');

    userService['updateMe'].mockReturnValue(of(me));
    (component as any).saveProfile();
    expect(component['profileError']()).toBe('');
    expect(component['profileSuccess']()).toBe('Profile updated.');
  });

  it('scopes an error to the acting form only', () => {
    userService['deleteMe'].mockReturnValue(throwError(() => 'Wrong password'));
    ctrl('closeForm', 'password').setValue('nope');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    (component as any).closeAccount();
    expect(component['closeError']()).toBe('Wrong password');
    expect(component['profileError']()).toBe('');
    expect(component['passwordError']()).toBe('');
  });
});
