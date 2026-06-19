import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/services/auth.service';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let auth: { login: ReturnType<typeof vi.fn> };
  let router: Router;

  beforeEach(() => {
    auth = { login: vi.fn() };
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('does not submit an invalid form', () => {
    component.submit();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('navigates on successful login', () => {
    const navSpy = vi.spyOn(router, 'navigateByUrl').mockReturnValue(Promise.resolve(true));
    auth.login.mockReturnValue(of({}));
    component.form.setValue({ email: 'a@b.com', password: 'secret12' });
    component.submit();
    expect(auth.login).toHaveBeenCalled();
    expect(navSpy).toHaveBeenCalled();
  });

  it('shows an error message on failed login', () => {
    auth.login.mockReturnValue(throwError(() => 'Invalid credentials'));
    component.form.setValue({ email: 'a@b.com', password: 'secret12' });
    component.submit();
    expect(component.error()).toBe('Invalid credentials');
  });
});
