import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import { AuthResponse } from '../models/auth.model';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/v1/Auth`;

  const authResponse: AuthResponse = {
    accessToken: 'access-123',
    refreshToken: 'refresh-456',
    accessTokenExpiry: '2026-06-19T12:00:00',
    user: { id: 1, name: 'Jo', email: 'jo@x.com', phone: '123', role: 'User', isActive: true, createdAt: '2026-06-19T00:00:00' },
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), AuthService],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.role()).toBeNull();
  });

  it('login stores tokens and populates currentUser', () => {
    service.login({ email: 'jo@x.com', password: 'pw' }).subscribe();
    const req = http.expectOne(`${base}/login`);
    expect(req.request.method).toBe('POST');
    req.flush(authResponse);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.role()).toBe('User');
    expect(service.accessToken()).toBe('access-123');
    expect(localStorage.getItem('ems_refresh_token')).toBe('refresh-456');
  });

  it('login surfaces the backend error message as a string', () => {
    let received: unknown;
    service.login({ email: 'jo@x.com', password: 'wrong' }).subscribe({
      error: (msg) => { received = msg; },
    });
    http.expectOne(`${base}/login`).flush(
      { error: 'Invalid email or password.' },
      { status: 401, statusText: 'Unauthorized' },
    );
    expect(received).toBe('Invalid email or password.');
  });

  it('logout clears state and storage', () => {
    service.login({ email: 'jo@x.com', password: 'pw' }).subscribe();
    http.expectOne(`${base}/login`).flush(authResponse);

    service.logout();

    // logout() fires a POST to /logout; expect and flush it so http.verify() passes
    const logoutReq = http.expectOne(`${base}/logout`);
    logoutReq.flush(null);

    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken()).toBeNull();
    expect(localStorage.getItem('ems_access_token')).toBeNull();
  });

  it('refresh posts stored refresh token and updates tokens', () => {
    localStorage.setItem('ems_refresh_token', 'refresh-456');
    service.refresh().subscribe();
    const req = http.expectOne(`${base}/refresh`);
    expect(req.request.body).toEqual({ refreshToken: 'refresh-456' });
    req.flush({ ...authResponse, accessToken: 'new-access' });
    expect(service.accessToken()).toBe('new-access');
  });
});
