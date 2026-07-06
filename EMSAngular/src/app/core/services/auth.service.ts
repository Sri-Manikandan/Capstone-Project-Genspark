import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, finalize, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { extractError } from './http-error';
import { User, Role } from '../models/user.model';
import {
  AuthResponse, LoginRequest, RegisterRequest, ForgotPasswordRequest,
  ForgotPasswordResponse, ResetPasswordRequest,
} from '../models/auth.model';

const ACCESS_KEY = 'ems_access_token';
const REFRESH_KEY = 'ems_refresh_token';
const USER_KEY = 'ems_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Auth`;

  private userSignal = signal<User | null>(this.readStoredUser());
  readonly currentUser = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => !!this.userSignal());
  readonly role = computed<Role | null>(() => this.userSignal()?.role ?? null);

  private refreshInFlight: Observable<AuthResponse> | null = null;

  accessToken(): string | null { return localStorage.getItem(ACCESS_KEY); }

  /** True when a JWT access token is stored and its exp claim is in the past. */
  isAccessTokenExpired(): boolean {
    const token = localStorage.getItem(ACCESS_KEY);
    if (!token) return false;
    const expiryMs = this.tokenExpiryMs(token);
    if (expiryMs === null) return false; // opaque/unparseable token — let the reactive path handle it
    return Date.now() >= expiryMs - 5000; // 5s clock-skew buffer
  }

  /** Refresh, sharing a single in-flight call so concurrent requests don't stampede. */
  refreshShared(): Observable<AuthResponse> {
    this.refreshInFlight ??= this.refresh().pipe(
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, req)
      .pipe(tap(res => this.applyAuth(res)), catchError(e => throwError(() => extractError(e))));
  }

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/register`, req)
      .pipe(tap(res => this.applyAuth(res)), catchError(e => throwError(() => extractError(e))));
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = localStorage.getItem(REFRESH_KEY) ?? '';
    return this.http.post<AuthResponse>(`${this.base}/refresh`, { refreshToken })
      .pipe(tap(res => this.applyAuth(res)));
  }

  logout(): void {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (refreshToken) {
      this.http.post<void>(`${this.base}/logout`, { refreshToken }).subscribe({ error: () => {} });
    }
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    this.userSignal.set(null);
  }

  forgotPassword(req: ForgotPasswordRequest): Observable<ForgotPasswordResponse> {
    return this.http.post<ForgotPasswordResponse>(`${this.base}/forgot-password`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  resetPassword(req: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/reset-password`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  setCurrentUser(user: User): void {
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    this.userSignal.set(user);
  }

  private applyAuth(res: AuthResponse): void {
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    this.userSignal.set(res.user);
  }

  private tokenExpiryMs(token: string): number | null {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    try {
      let b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      b64 += '='.repeat((4 - (b64.length % 4)) % 4);
      const payload = JSON.parse(atob(b64));
      return typeof payload.exp === 'number' ? payload.exp * 1000 : null;
    } catch {
      return null;
    }
  }

  private readStoredUser(): User | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw) as User; } catch { return null; }
  }
}
