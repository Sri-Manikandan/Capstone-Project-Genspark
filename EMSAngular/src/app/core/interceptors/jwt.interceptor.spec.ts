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
});
