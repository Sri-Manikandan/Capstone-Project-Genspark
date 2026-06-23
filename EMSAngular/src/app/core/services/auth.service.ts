import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
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

  accessToken(): string | null { return localStorage.getItem(ACCESS_KEY); }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, req)
      .pipe(tap(res => this.applyAuth(res)));
  }

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/register`, req)
      .pipe(tap(res => this.applyAuth(res)));
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
    return this.http.post<ForgotPasswordResponse>(`${this.base}/forgot-password`, req);
  }

  resetPassword(req: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/reset-password`, req);
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

  private readStoredUser(): User | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw) as User; } catch { return null; }
  }
}
