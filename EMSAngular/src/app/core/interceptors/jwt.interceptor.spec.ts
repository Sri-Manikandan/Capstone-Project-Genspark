import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { jwtInterceptor } from './jwt.interceptor';
import { environment } from '../../../environments/environment';

describe('jwtInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([jwtInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  it('attaches bearer token to api calls', () => {
    localStorage.setItem('ems_access_token', 'tok-1');
    http.get(`${environment.apiBaseUrl}/api/v1/Event`).subscribe();
    const req = ctrl.expectOne(`${environment.apiBaseUrl}/api/v1/Event`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok-1');
    req.flush({});
  });

  it('does not attach token to login', () => {
    localStorage.setItem('ems_access_token', 'tok-1');
    http.post(`${environment.apiBaseUrl}/api/v1/Auth/login`, {}).subscribe();
    const req = ctrl.expectOne(`${environment.apiBaseUrl}/api/v1/Auth/login`);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('proactively refreshes an expired token before attaching it', () => {
    const now = Math.floor(Date.now() / 1000);
    localStorage.setItem('ems_access_token', makeJwt(now - 60));
    localStorage.setItem('ems_refresh_token', 'refresh-1');

    http.post(`${environment.apiBaseUrl}/api/v1/Seat/reserve`, {}).subscribe();

    // The interceptor refreshes first...
    const refreshReq = ctrl.expectOne(`${environment.apiBaseUrl}/api/v1/Auth/refresh`);
    refreshReq.flush({
      accessToken: 'fresh-token', refreshToken: 'refresh-2', accessTokenExpiry: '',
      user: { id: 1, name: 'Jo', email: 'jo@x.com', phone: '1', role: 'User', isActive: true, createdAt: '' },
    });

    // ...then sends the original request carrying the new token.
    const apiReq = ctrl.expectOne(`${environment.apiBaseUrl}/api/v1/Seat/reserve`);
    expect(apiReq.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    apiReq.flush({});
  });
});

function makeJwt(expSeconds: number): string {
  const b64url = (o: object) =>
    btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64url({ alg: 'HS256' })}.${b64url({ exp: expSeconds })}.sig`;
}
