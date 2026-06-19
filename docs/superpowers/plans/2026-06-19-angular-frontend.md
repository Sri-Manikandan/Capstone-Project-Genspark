# Angular Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a responsive Angular SPA serving User, Organizer, and Admin roles against the existing EMS REST API.

**Architecture:** Feature modules with lazy loading. Angular Signals for state inside injectable services (no NgRx). Tailwind CSS for a clean/minimal light theme. Stripe Payment Element for checkout. SignalR for real-time seat updates.

**Tech Stack:** Angular 18+, TypeScript, Tailwind CSS, RxJS, `@microsoft/signalr`, `@stripe/stripe-js`, Jasmine + Karma.

## Global Constraints

- **API base URL:** `http://localhost:5222` (verified from `launchSettings.json`). Set in `environments/environment.ts` as `apiBaseUrl`; all service calls use `${environment.apiBaseUrl}/api/v1/...`.
- **SignalR hub:** `http://localhost:5222/hubs/seats`.
- **Stripe publishable key:** `environment.stripePublishableKey`.
- **Auth token storage keys:** `localStorage` keys `ems_access_token` and `ems_refresh_token`.
- **Git commit messages must be 5 words or fewer.** No body, no bullets, no co-author lines (per project CLAUDE.md).
- **DI style:** use `inject()`, never constructor injection.
- **Component naming:** files kebab-case (`event-list.ts`); classes PascalCase + suffix (`EventListComponent`); component selectors prefixed `ems-`.
- **Member ordering:** `inject()` calls → `input()`/`output()`/`model()` → queries → other props → lifecycle hooks → methods. `protected` for template-only members; `readonly` on inputs/outputs/queries.
- **All datetimes from the API are already converted to IST by the backend** (`TimeHelper.UtcToIst` in the mapping profile). The frontend `IstDatePipe` only formats; it does NOT re-convert timezones.
- **Roles:** `Admin`, `Organizer`, `User` (exact casing).
- **Event statuses:** `Draft`, `PendingApproval`, `Published`, `Rejected`, `Cancelled`.
- **Booking statuses:** `Pending`, `Confirmed`, `Cancelled`, `Attended`.
- **Paged responses** have shape `{ items: T[], totalCount, page, pageSize, totalPages }`.

---

## File Structure

```
EMSAngular/src/
├── environments/
│   ├── environment.ts                  base URL, stripe key (dev)
│   └── environment.prod.ts
├── styles.css                          Tailwind directives + base layer
├── app/
│   ├── core/
│   │   ├── models/                      one file per domain (interfaces mirroring DTOs)
│   │   ├── services/                    one service per domain + auth + seat-hub
│   │   ├── guards/                      auth.guard.ts, role.guard.ts
│   │   └── interceptors/                jwt.interceptor.ts, auth-error.interceptor.ts
│   ├── shared/
│   │   ├── components/                  navbar, event-card, seat-map, stripe-payment,
│   │   │                                booking-qr, pagination, loading-spinner, alert
│   │   ├── pipes/                        ist-date.pipe.ts, currency-inr.pipe.ts
│   │   └── shared.module.ts
│   ├── features/
│   │   ├── auth/                         login, register, forgot/reset password
│   │   ├── events/                       browse, detail, checkout
│   │   ├── bookings/                     list, detail
│   │   ├── organizer/                    events CRUD, ticket types, scanner
│   │   └── admin/                        users, venues, seats, approvals, requests
│   ├── app.component.ts                  shell (navbar + router-outlet)
│   ├── app.module.ts
│   └── app-routing.module.ts            lazy-loads all 5 feature modules
```

---

## Phase 0 — Project Scaffolding

### Task 0: Scaffold Angular app with Tailwind, env, and dependencies

**Files:**
- Create: `EMSAngular/` (entire Angular workspace)
- Create: `EMSAngular/src/environments/environment.ts`
- Create: `EMSAngular/src/environments/environment.prod.ts`
- Modify: `EMSAngular/src/styles.css`
- Modify: `EMSAngular/tailwind.config.js`
- Modify: `EMSAngular/src/index.html`

**Interfaces:**
- Produces: `environment.apiBaseUrl`, `environment.stripePublishableKey` consumed by every service.

- [ ] **Step 1: Generate the workspace** (run from repo root `Capstone Project/`)

```bash
npx -p @angular/cli@latest ng new EMSAngular \
  --routing=true --style=css --ssr=false --skip-git=true --package-manager=npm
```

- [ ] **Step 2: Install runtime dependencies**

```bash
cd EMSAngular
npm install @microsoft/signalr @stripe/stripe-js
npm install -D tailwindcss@^3 postcss autoprefixer
npx tailwindcss init
```

- [ ] **Step 3: Configure Tailwind** — replace `tailwind.config.js` content:

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: { extend: {} },
  plugins: [],
};
```

- [ ] **Step 4: Add Tailwind directives** — replace `src/styles.css` content:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

html, body { @apply bg-gray-50 text-gray-900; height: 100%; }
```

- [ ] **Step 5: Create `src/environments/environment.ts`**

```ts
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5222',
  stripePublishableKey: 'pk_test_REPLACE_WITH_YOUR_KEY',
};
```

- [ ] **Step 6: Create `src/environments/environment.prod.ts`**

```ts
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:5222',
  stripePublishableKey: 'pk_test_REPLACE_WITH_YOUR_KEY',
};
```

- [ ] **Step 7: Verify build and the default test pass**

Run: `cd EMSAngular && ng build && ng test --watch=false --browsers=ChromeHeadless`
Expected: build succeeds; default `AppComponent` spec passes.

- [ ] **Step 8: Commit**

```bash
git add EMSAngular
git commit -m "scaffold angular app"
```

---

## Phase 1 — Core Models

These are pure TypeScript interfaces mirroring the backend DTOs. No tests (no logic). All created in one task, one commit.

### Task 1: Create domain model interfaces

**Files:**
- Create: `EMSAngular/src/app/core/models/paged-result.model.ts`
- Create: `EMSAngular/src/app/core/models/user.model.ts`
- Create: `EMSAngular/src/app/core/models/auth.model.ts`
- Create: `EMSAngular/src/app/core/models/event.model.ts`
- Create: `EMSAngular/src/app/core/models/venue.model.ts`
- Create: `EMSAngular/src/app/core/models/seat.model.ts`
- Create: `EMSAngular/src/app/core/models/ticket-type.model.ts`
- Create: `EMSAngular/src/app/core/models/booking.model.ts`
- Create: `EMSAngular/src/app/core/models/payment.model.ts`
- Create: `EMSAngular/src/app/core/models/admin.model.ts`

**Interfaces:**
- Produces: all interfaces below, consumed by every service and component.

- [ ] **Step 1: `paged-result.model.ts`**

```ts
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```

- [ ] **Step 2: `user.model.ts`**

```ts
export type Role = 'User' | 'Organizer' | 'Admin';

export interface User {
  id: number;
  name: string;
  email: string;
  phone: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export interface UpdateUserRequest { name: string; phone: string; }
export interface ChangePasswordRequest { currentPassword: string; newPassword: string; }
export interface ChangeEmailRequest { newEmail: string; password: string; }
export interface CloseAccountRequest { password: string; }
export interface UserSearchRequest {
  query?: string; role?: Role; isActive?: boolean; page: number; pageSize: number;
}
```

- [ ] **Step 3: `auth.model.ts`**

```ts
import { User } from './user.model';

export interface RegisterRequest {
  name: string; email: string; phone: string; password: string; role?: string;
}
export interface LoginRequest { email: string; password: string; }
export interface RefreshTokenRequest { refreshToken: string; }
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
  user: User;
}
export interface ForgotPasswordRequest { email: string; }
export interface ForgotPasswordResponse { message: string; resetToken: string; }
export interface ResetPasswordRequest { token: string; newPassword: string; }
```

- [ ] **Step 4: `event.model.ts`**

```ts
export type EventStatus =
  | 'Draft' | 'PendingApproval' | 'Published' | 'Rejected' | 'Cancelled';

export interface EventDto {
  id: number;
  organizerId: number;
  venueId: number;
  title: string;
  description: string;
  status: EventStatus;
  rejectionReason?: string | null;
  startTime: string;
  endTime: string;
  imageUrl: string;
  category: string;
  slug: string;
  createdAt: string;
}

export interface CreateEventRequest {
  venueId: number; title: string; description: string;
  startTime: string; endTime: string; imageUrl: string; category: string;
}
export interface UpdateEventRequest {
  title: string; description: string;
  startTime: string; endTime: string; imageUrl: string; category: string;
}
export interface EventSearchRequest {
  query?: string; category?: string; status?: string;
  startFrom?: string; startTo?: string;
  sortBy?: 'title' | 'startTime' | 'createdAt';
  sortOrder?: 'asc' | 'desc';
  page: number; pageSize: number;
}
```

- [ ] **Step 5: `venue.model.ts`**

```ts
export interface VenueDto {
  id: number; name: string; address: string; city: string;
  totalCapacity: number; layoutConfig: string; createdAt: string;
}
export interface CreateVenueRequest {
  name: string; address: string; city: string;
  totalCapacity: number; layoutConfig: string;
}
export interface UpdateVenueRequest extends CreateVenueRequest {}
```

- [ ] **Step 6: `seat.model.ts`**

```ts
export interface SeatDto {
  id: number; venueId: number; section: string; row: string;
  seatNumber: number; seatType: string;
}
export interface CreateSeatRequest {
  venueId: number; section: string; row: string;
  seatNumber: number; seatType: string;
}
export interface BulkCreateSeatsRequest {
  venueId: number; section: string; row: string;
  startNumber: number; endNumber: number; seatType: string;
}
export interface ReserveSeatRequest {
  eventId: number; seatId: number; ticketTypeId: number;
}
export interface SeatReservationDto {
  id: number; seatId: number; eventId: number; ticketTypeId: number;
  userId: number; status: string; reservedUntil: string; createdAt: string;
}
```

- [ ] **Step 7: `ticket-type.model.ts`**

```ts
export interface TicketTypeDto {
  id: number; eventId: number; name: string; seatType: string;
  price: number; totalQuantity: number; availableQuantity: number;
  saleStart: string; saleEnd: string; isActive: boolean; createdAt: string;
}
export interface CreateTicketTypeRequest {
  eventId: number; name: string; seatType: string; price: number;
  totalQuantity: number; saleStart: string; saleEnd: string;
}
export interface UpdateTicketTypeRequest {
  name: string; seatType: string; price: number; totalQuantity: number;
  saleStart: string; saleEnd: string; isActive: boolean;
}
```

- [ ] **Step 8: `booking.model.ts`**

```ts
export type BookingStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Attended';

export interface BookingItemRequest { ticketTypeId: number; seatId: number; }
export interface CreateBookingRequest { eventId: number; items: BookingItemRequest[]; }

export interface BookingItemDto {
  id: number; ticketTypeId: number; ticketTypeName: string;
  seatId: number; seatLabel: string; unitPrice: number; ticketStatus: string;
}
export interface BookingDto {
  id: number; userId: number; eventId: number; eventTitle: string;
  bookingReference: string; qrCode: string; totalAmount: number;
  bookingStatus: BookingStatus; expiresAt: string; createdAt: string;
  items: BookingItemDto[];
}
export interface BookingQueryRequest { status?: BookingStatus; page: number; pageSize: number; }
export interface ValidateQrRequest { qrPayload: string; scannedBy: number; }
```

- [ ] **Step 9: `payment.model.ts`**

```ts
export interface PaymentDto {
  id: number; bookingId: number; stripePaymentIntentId: string;
  amount: number; currency: string; status: string;
  paidAt?: string | null; createdAt: string;
}
export interface PaymentInitiateDto extends PaymentDto { clientSecret: string; }
export interface InitiatePaymentRequest { bookingId: number; currency: string; }
export interface ConfirmPaymentRequest { stripePaymentIntentId: string; }
```

- [ ] **Step 10: `admin.model.ts`**

```ts
export interface OrganizerRequestDto {
  id: number; userId: number; userName: string; userEmail: string;
  status: string; reason?: string | null;
  requestedAt: string; reviewedAt?: string | null; reviewedByAdminId?: number | null;
}
export interface ReviewRequest { reason?: string; }
export interface OrganizerRequestQueryRequest { status?: string; page: number; pageSize: number; }
```

- [ ] **Step 11: Verify compilation**

Run: `cd EMSAngular && ng build`
Expected: build succeeds (no unused-import or type errors).

- [ ] **Step 12: Commit**

```bash
git add EMSAngular/src/app/core/models
git commit -m "add core model interfaces"
```

---

## Phase 2 — Core Services, Interceptors, Guards

### Task 2: AuthService

**Files:**
- Create: `EMSAngular/src/app/core/services/auth.service.ts`
- Test: `EMSAngular/src/app/core/services/auth.service.spec.ts`

**Interfaces:**
- Consumes: `environment.apiBaseUrl`; `AuthResponse`, `LoginRequest`, `RegisterRequest`, `ForgotPasswordRequest`, `ForgotPasswordResponse`, `ResetPasswordRequest` from models; `User`, `Role`.
- Produces:
  - `currentUser: Signal<User | null>`
  - `isAuthenticated: Signal<boolean>`
  - `role: Signal<Role | null>`
  - `accessToken(): string | null`
  - `login(req: LoginRequest): Observable<AuthResponse>`
  - `register(req: RegisterRequest): Observable<AuthResponse>`
  - `refresh(): Observable<AuthResponse>`
  - `logout(): void`
  - `forgotPassword(req: ForgotPasswordRequest): Observable<ForgotPasswordResponse>`
  - `resetPassword(req: ResetPasswordRequest): Observable<void>`
  - storage keys: `ems_access_token`, `ems_refresh_token`, `ems_user`

- [ ] **Step 1: Write the failing test** — `auth.service.spec.ts`

```ts
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
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
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [AuthService] });
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

  it('logout clears state and storage', () => {
    service.login({ email: 'jo@x.com', password: 'pw' }).subscribe();
    http.expectOne(`${base}/login`).flush(authResponse);

    service.logout();
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/auth.service.spec.ts'`
Expected: FAIL — `AuthService` does not exist.

- [ ] **Step 3: Write the implementation** — `auth.service.ts`

```ts
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/auth.service.spec.ts'`
Expected: PASS (all specs).

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/core/services/auth.service.ts EMSAngular/src/app/core/services/auth.service.spec.ts
git commit -m "add auth service"
```

---

### Task 3: JwtInterceptor and AuthErrorInterceptor

**Files:**
- Create: `EMSAngular/src/app/core/interceptors/jwt.interceptor.ts`
- Create: `EMSAngular/src/app/core/interceptors/auth-error.interceptor.ts`
- Test: `EMSAngular/src/app/core/interceptors/jwt.interceptor.spec.ts`

**Interfaces:**
- Consumes: `AuthService.accessToken()`, `AuthService.refresh()`, `AuthService.logout()`.
- Produces: two `HttpInterceptorFn` exports: `jwtInterceptor`, `authErrorInterceptor`. Registered in `app.module.ts` via `provideHttpClient(withInterceptors([...]))`.
- Note: skips attaching the header for requests to `/api/v1/Auth/login`, `/register`, `/refresh`, `/forgot-password`, `/reset-password`.

- [ ] **Step 1: Write the failing test** — `jwt.interceptor.spec.ts`

```ts
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/jwt.interceptor.spec.ts'`
Expected: FAIL — `jwtInterceptor` not found.

- [ ] **Step 3: Implement `jwt.interceptor.ts`**

```ts
import { HttpInterceptorFn } from '@angular/common/http';

const AUTH_PATHS = ['/Auth/login', '/Auth/register', '/Auth/refresh',
  '/Auth/forgot-password', '/Auth/reset-password'];

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const isAuthPath = AUTH_PATHS.some(p => req.url.includes(p));
  const token = localStorage.getItem('ems_access_token');
  if (token && !isAuthPath) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
  return next(req);
};
```

- [ ] **Step 4: Implement `auth-error.interceptor.ts`**

```ts
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isRefreshCall = req.url.includes('/Auth/refresh');

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !isRefreshCall && localStorage.getItem('ems_refresh_token')) {
        return auth.refresh().pipe(
          switchMap(res => next(req.clone({
            setHeaders: { Authorization: `Bearer ${res.accessToken}` },
          }))),
          catchError(refreshErr => {
            auth.logout();
            router.navigate(['/auth/login']);
            return throwError(() => refreshErr);
          }),
        );
      }
      return throwError(() => err);
    }),
  );
};
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/jwt.interceptor.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/core/interceptors
git commit -m "add http interceptors"
```

---

### Task 4: AuthGuard and RoleGuard

**Files:**
- Create: `EMSAngular/src/app/core/guards/auth.guard.ts`
- Create: `EMSAngular/src/app/core/guards/role.guard.ts`
- Test: `EMSAngular/src/app/core/guards/guards.spec.ts`

**Interfaces:**
- Consumes: `AuthService.isAuthenticated()`, `AuthService.role()`.
- Produces:
  - `authGuard: CanActivateFn` — returns `true` if authenticated, else a `UrlTree` to `/auth/login` with `returnUrl`.
  - `roleGuard: CanActivateFn` — reads `route.data['roles'] as Role[]`; returns `true` if `role()` is included, else `UrlTree` to `/`.

- [ ] **Step 1: Write the failing test** — `guards.spec.ts`

```ts
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { authGuard } from './auth.guard';
import { roleGuard } from './role.guard';
import { AuthService } from '../services/auth.service';

function runGuard(guard: any, route: any = { data: {} }, state: any = { url: '/x' }) {
  return TestBed.runInInjectionContext(() => guard(route, state));
}

describe('guards', () => {
  let authStub: { isAuthenticated: any; role: any };

  beforeEach(() => {
    authStub = { isAuthenticated: signal(false), role: signal<string | null>(null) };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: authStub }],
    });
  });

  it('authGuard blocks unauthenticated with a UrlTree', () => {
    const result = runGuard(authGuard, { data: {} }, { url: '/bookings' });
    expect(result instanceof UrlTree).toBe(true);
  });

  it('authGuard allows authenticated', () => {
    authStub.isAuthenticated.set(true);
    expect(runGuard(authGuard)).toBe(true);
  });

  it('roleGuard allows matching role', () => {
    authStub.isAuthenticated.set(true);
    authStub.role.set('Admin');
    expect(runGuard(roleGuard, { data: { roles: ['Admin'] } })).toBe(true);
  });

  it('roleGuard blocks non-matching role with UrlTree', () => {
    authStub.isAuthenticated.set(true);
    authStub.role.set('User');
    const result = runGuard(roleGuard, { data: { roles: ['Admin'] } });
    expect(result instanceof UrlTree).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/guards.spec.ts'`
Expected: FAIL — guards not found.

- [ ] **Step 3: Implement `auth.guard.ts`**

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  return router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } });
};
```

- [ ] **Step 4: Implement `role.guard.ts`**

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { Role } from '../models/user.model';

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const allowed = (route.data['roles'] as Role[]) ?? [];
  const current = auth.role();
  if (current && allowed.includes(current)) return true;
  return router.createUrlTree(['/']);
};
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/guards.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/core/guards
git commit -m "add route guards"
```

---

### Task 5: HTTP error extraction helper + EventService

**Files:**
- Create: `EMSAngular/src/app/core/services/http-error.ts`
- Create: `EMSAngular/src/app/core/services/event.service.ts`
- Test: `EMSAngular/src/app/core/services/event.service.spec.ts`

**Interfaces:**
- Produces:
  - `extractError(err: HttpErrorResponse): string` — returns `err.error?.message ?? err.error?.error ?? err.message ?? 'Unexpected error'`. Used by every service via `catchError(e => throwError(() => extractError(e)))`.
  - `toHttpParams(obj: Record<string, unknown>): HttpParams` — builds query params, skipping `undefined`/`null`/`''` values.
  - `EventService` methods:
    - `search(req: EventSearchRequest): Observable<PagedResult<EventDto>>` → GET `/api/v1/Event`
    - `getById(id: number): Observable<EventDto>` → GET `/api/v1/Event/{id}`
    - `getBySlug(slug: string): Observable<EventDto>` → GET `/api/v1/Event/slug/{slug}`
    - `getMyEvents(page: number, pageSize: number): Observable<PagedResult<EventDto>>` → GET `/api/v1/Event/my`
    - `create(req: CreateEventRequest): Observable<EventDto>` → POST `/api/v1/Event`
    - `update(id: number, req: UpdateEventRequest): Observable<EventDto>` → PUT `/api/v1/Event/{id}`
    - `delete(id: number): Observable<void>` → DELETE `/api/v1/Event/{id}`
    - `submit(id: number): Observable<EventDto>` → POST `/api/v1/Event/{id}/submit`
    - `cancel(id: number): Observable<EventDto>` → POST `/api/v1/Event/{id}/cancel`

- [ ] **Step 1: Write the failing test** — `event.service.spec.ts`

```ts
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { EventService } from './event.service';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import { EventDto } from '../models/event.model';

describe('EventService', () => {
  let service: EventService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/v1/Event`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [EventService] });
    service = TestBed.inject(EventService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('search builds query params and returns paged result', () => {
    const paged: PagedResult<EventDto> = { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 };
    service.search({ query: 'rock', page: 1, pageSize: 10 }).subscribe(r => expect(r).toEqual(paged));
    const req = http.expectOne(r => r.url === base);
    expect(req.request.params.get('query')).toBe('rock');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(paged);
  });

  it('getBySlug hits slug endpoint', () => {
    service.getBySlug('my-event').subscribe();
    http.expectOne(`${base}/slug/my-event`).flush({} as EventDto);
  });

  it('submit posts to submit endpoint', () => {
    service.submit(7).subscribe();
    const req = http.expectOne(`${base}/7/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({} as EventDto);
  });

  it('maps error to string message', () => {
    service.getById(99).subscribe({ error: e => expect(e).toBe('Event not found.') });
    http.expectOne(`${base}/99`).flush({ message: 'Event not found.' }, { status: 404, statusText: 'Not Found' });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event.service.spec.ts'`
Expected: FAIL — `EventService` not found.

- [ ] **Step 3: Implement `http-error.ts`**

```ts
import { HttpErrorResponse, HttpParams } from '@angular/common/http';

export function extractError(err: HttpErrorResponse): string {
  return err.error?.message ?? err.error?.error ?? err.message ?? 'Unexpected error';
}

export function toHttpParams(obj: Record<string, unknown>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(obj)) {
    if (value === undefined || value === null || value === '') continue;
    params = params.set(key, String(value));
  }
  return params;
}
```

- [ ] **Step 4: Implement `event.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import {
  EventDto, CreateEventRequest, UpdateEventRequest, EventSearchRequest,
} from '../models/event.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class EventService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Event`;

  search(req: EventSearchRequest): Observable<PagedResult<EventDto>> {
    return this.http.get<PagedResult<EventDto>>(this.base, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  getById(id: number): Observable<EventDto> {
    return this.http.get<EventDto>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  getBySlug(slug: string): Observable<EventDto> {
    return this.http.get<EventDto>(`${this.base}/slug/${slug}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  getMyEvents(page: number, pageSize: number): Observable<PagedResult<EventDto>> {
    return this.http.get<PagedResult<EventDto>>(`${this.base}/my`, { params: toHttpParams({ page, pageSize }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  create(req: CreateEventRequest): Observable<EventDto> {
    return this.http.post<EventDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  update(id: number, req: UpdateEventRequest): Observable<EventDto> {
    return this.http.put<EventDto>(`${this.base}/${id}`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  submit(id: number): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/${id}/submit`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
  cancel(id: number): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/${id}/cancel`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event.service.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/core/services/http-error.ts EMSAngular/src/app/core/services/event.service.ts EMSAngular/src/app/core/services/event.service.spec.ts
git commit -m "add event service"
```

---

### Task 6: BookingService, PaymentService, SeatService, TicketTypeService, VenueService, UserService, AdminService

Each follows the exact same pattern as `EventService` (inject `HttpClient`, `catchError` → `extractError`). Endpoints below are verified against the controllers. Write one combined spec asserting the URL + method for one representative method per service, then implement all seven.

**Files:**
- Create: `booking.service.ts`, `payment.service.ts`, `seat.service.ts`, `ticket-type.service.ts`, `venue.service.ts`, `user.service.ts`, `admin.service.ts` (all under `EMSAngular/src/app/core/services/`)
- Test: `EMSAngular/src/app/core/services/domain-services.spec.ts`

**Interfaces:**
- `BookingService` (base `/api/v1/Booking`):
  - `create(req: CreateBookingRequest): Observable<BookingDto>` → POST `/`
  - `getById(id: number): Observable<BookingDto>` → GET `/{id}`
  - `getByReference(reference: string): Observable<BookingDto>` → GET `/reference/{reference}`
  - `getMyBookings(req: BookingQueryRequest): Observable<PagedResult<BookingDto>>` → GET `/my`
  - `getByEvent(eventId: number, req: BookingQueryRequest): Observable<PagedResult<BookingDto>>` → GET `/event/{eventId}`
  - `cancel(id: number): Observable<BookingDto>` → POST `/{id}/cancel`
  - `validateQr(req: ValidateQrRequest): Observable<BookingDto>` → POST `/validate-qr`
- `PaymentService` (base `/api/v1/Payment`):
  - `initiate(req: InitiatePaymentRequest): Observable<PaymentInitiateDto>` → POST `/initiate`
  - `confirm(req: ConfirmPaymentRequest): Observable<PaymentDto>` → POST `/confirm`
  - `getByBooking(bookingId: number): Observable<PaymentDto>` → GET `/booking/{bookingId}`
- `SeatService` (base `/api/v1/Seat`):
  - `getByVenue(venueId: number): Observable<SeatDto[]>` → GET `/venue/{venueId}`
  - `getAvailableByEvent(eventId: number): Observable<SeatDto[]>` → GET `/available/event/{eventId}`
  - `create(req: CreateSeatRequest): Observable<SeatDto>` → POST `/`
  - `bulkCreate(req: BulkCreateSeatsRequest): Observable<SeatDto[]>` → POST `/bulk`
  - `delete(id: number): Observable<void>` → DELETE `/{id}`
  - `reserve(req: ReserveSeatRequest): Observable<SeatReservationDto>` → POST `/reserve`
  - `releaseReservation(reservationId: number): Observable<void>` → POST `/reserve/{reservationId}/release`
- `TicketTypeService` (base `/api/v1/TicketType`):
  - `getByEvent(eventId: number): Observable<TicketTypeDto[]>` → GET `/event/{eventId}`
  - `getActiveByEvent(eventId: number): Observable<TicketTypeDto[]>` → GET `/event/{eventId}/active`
  - `getById(id: number): Observable<TicketTypeDto>` → GET `/{id}`
  - `create(req: CreateTicketTypeRequest): Observable<TicketTypeDto>` → POST `/`
  - `update(id: number, req: UpdateTicketTypeRequest): Observable<TicketTypeDto>` → PUT `/{id}`
  - `delete(id: number): Observable<void>` → DELETE `/{id}`
- `VenueService` (base `/api/v1/Venue`):
  - `list(): Observable<VenueDto[]>` → GET `/`
  - `getById(id: number): Observable<VenueDto>` → GET `/{id}`
  - `create(req: CreateVenueRequest): Observable<VenueDto>` → POST `/`
  - `update(id: number, req: UpdateVenueRequest): Observable<VenueDto>` → PUT `/{id}`
  - `delete(id: number): Observable<void>` → DELETE `/{id}`
- `UserService` (base `/api/v1/User`):
  - `getMe(): Observable<User>` → GET `/me`
  - `updateMe(req: UpdateUserRequest): Observable<User>` → PUT `/me`
  - `changePassword(req: ChangePasswordRequest): Observable<void>` → PUT `/me/password`
  - `changeEmail(req: ChangeEmailRequest): Observable<User>` → PUT `/me/email`
  - `deleteMe(req: CloseAccountRequest): Observable<void>` → DELETE `/me` (body via `{ body: req }`)
  - `search(req: UserSearchRequest): Observable<PagedResult<User>>` → GET `/`
  - `getById(id: number): Observable<User>` → GET `/{id}`
  - `deleteById(id: number): Observable<void>` → DELETE `/{id}`
  - `requestOrganizer(reason: string): Observable<OrganizerRequestDto>` → POST `/request-organizer` body `{ reason }`
  - `getOrganizerRequest(): Observable<OrganizerRequestDto>` → GET `/organizer-request`
- `AdminService` (base `/api/v1/Admin`):
  - `getOrganizerRequests(req: OrganizerRequestQueryRequest): Observable<PagedResult<OrganizerRequestDto>>` → GET `/organizer-requests`
  - `approveOrganizerRequest(id: number, req: ReviewRequest): Observable<OrganizerRequestDto>` → POST `/organizer-requests/{id}/approve`
  - `rejectOrganizerRequest(id: number, req: ReviewRequest): Observable<OrganizerRequestDto>` → POST `/organizer-requests/{id}/reject`
  - `getPendingEvents(page: number, pageSize: number): Observable<PagedResult<EventDto>>` → GET `/events/pending`
  - `approveEvent(id: number, req: ReviewRequest): Observable<EventDto>` → POST `/events/{id}/approve`
  - `rejectEvent(id: number, req: ReviewRequest): Observable<EventDto>` → POST `/events/{id}/reject`
  - `getUsers(req: UserSearchRequest): Observable<PagedResult<User>>` → GET `/users`
  - `deleteUser(id: number): Observable<void>` → DELETE `/users/{id}`

- [ ] **Step 1: Write the failing spec** — `domain-services.spec.ts`

```ts
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { BookingService } from './booking.service';
import { PaymentService } from './payment.service';
import { SeatService } from './seat.service';
import { TicketTypeService } from './ticket-type.service';
import { VenueService } from './venue.service';
import { UserService } from './user.service';
import { AdminService } from './admin.service';

describe('domain services', () => {
  let http: HttpTestingController;
  const root = `${environment.apiBaseUrl}/api/v1`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('BookingService.create posts to /Booking', () => {
    TestBed.inject(BookingService).create({ eventId: 1, items: [] }).subscribe();
    const req = http.expectOne(`${root}/Booking`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('PaymentService.initiate posts to /Payment/initiate', () => {
    TestBed.inject(PaymentService).initiate({ bookingId: 1, currency: 'inr' }).subscribe();
    http.expectOne(`${root}/Payment/initiate`).flush({});
  });

  it('SeatService.reserve posts to /Seat/reserve', () => {
    TestBed.inject(SeatService).reserve({ eventId: 1, seatId: 2, ticketTypeId: 3 }).subscribe();
    http.expectOne(`${root}/Seat/reserve`).flush({});
  });

  it('TicketTypeService.getActiveByEvent hits active endpoint', () => {
    TestBed.inject(TicketTypeService).getActiveByEvent(5).subscribe();
    http.expectOne(`${root}/TicketType/event/5/active`).flush([]);
  });

  it('VenueService.list gets /Venue', () => {
    TestBed.inject(VenueService).list().subscribe();
    http.expectOne(`${root}/Venue`).flush([]);
  });

  it('UserService.getMe gets /User/me', () => {
    TestBed.inject(UserService).getMe().subscribe();
    http.expectOne(`${root}/User/me`).flush({});
  });

  it('AdminService.getPendingEvents gets /Admin/events/pending', () => {
    TestBed.inject(AdminService).getPendingEvents(1, 10).subscribe();
    const req = http.expectOne(r => r.url === `${root}/Admin/events/pending`);
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/domain-services.spec.ts'`
Expected: FAIL — services not found.

- [ ] **Step 3: Implement `booking.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import {
  BookingDto, CreateBookingRequest, BookingQueryRequest, ValidateQrRequest,
} from '../models/booking.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Booking`;

  create(req: CreateBookingRequest): Observable<BookingDto> {
    return this.http.post<BookingDto>(this.base, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getById(id: number): Observable<BookingDto> {
    return this.http.get<BookingDto>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getByReference(reference: string): Observable<BookingDto> {
    return this.http.get<BookingDto>(`${this.base}/reference/${reference}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getMyBookings(req: BookingQueryRequest): Observable<PagedResult<BookingDto>> {
    return this.http.get<PagedResult<BookingDto>>(`${this.base}/my`, { params: toHttpParams({ ...req }) }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getByEvent(eventId: number, req: BookingQueryRequest): Observable<PagedResult<BookingDto>> {
    return this.http.get<PagedResult<BookingDto>>(`${this.base}/event/${eventId}`, { params: toHttpParams({ ...req }) }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  cancel(id: number): Observable<BookingDto> {
    return this.http.post<BookingDto>(`${this.base}/${id}/cancel`, {}).pipe(catchError(e => throwError(() => extractError(e))));
  }
  validateQr(req: ValidateQrRequest): Observable<BookingDto> {
    return this.http.post<BookingDto>(`${this.base}/validate-qr`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 4: Implement `payment.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PaymentDto, PaymentInitiateDto, InitiatePaymentRequest, ConfirmPaymentRequest,
} from '../models/payment.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Payment`;

  initiate(req: InitiatePaymentRequest): Observable<PaymentInitiateDto> {
    return this.http.post<PaymentInitiateDto>(`${this.base}/initiate`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  confirm(req: ConfirmPaymentRequest): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(`${this.base}/confirm`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getByBooking(bookingId: number): Observable<PaymentDto> {
    return this.http.get<PaymentDto>(`${this.base}/booking/${bookingId}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 5: Implement `seat.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SeatDto, CreateSeatRequest, BulkCreateSeatsRequest, ReserveSeatRequest, SeatReservationDto,
} from '../models/seat.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class SeatService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Seat`;

  getByVenue(venueId: number): Observable<SeatDto[]> {
    return this.http.get<SeatDto[]>(`${this.base}/venue/${venueId}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getAvailableByEvent(eventId: number): Observable<SeatDto[]> {
    return this.http.get<SeatDto[]>(`${this.base}/available/event/${eventId}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  create(req: CreateSeatRequest): Observable<SeatDto> {
    return this.http.post<SeatDto>(this.base, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  bulkCreate(req: BulkCreateSeatsRequest): Observable<SeatDto[]> {
    return this.http.post<SeatDto[]>(`${this.base}/bulk`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  reserve(req: ReserveSeatRequest): Observable<SeatReservationDto> {
    return this.http.post<SeatReservationDto>(`${this.base}/reserve`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  releaseReservation(reservationId: number): Observable<void> {
    return this.http.post<void>(`${this.base}/reserve/${reservationId}/release`, {}).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 6: Implement `ticket-type.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TicketTypeDto, CreateTicketTypeRequest, UpdateTicketTypeRequest,
} from '../models/ticket-type.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class TicketTypeService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/TicketType`;

  getByEvent(eventId: number): Observable<TicketTypeDto[]> {
    return this.http.get<TicketTypeDto[]>(`${this.base}/event/${eventId}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getActiveByEvent(eventId: number): Observable<TicketTypeDto[]> {
    return this.http.get<TicketTypeDto[]>(`${this.base}/event/${eventId}/active`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getById(id: number): Observable<TicketTypeDto> {
    return this.http.get<TicketTypeDto>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  create(req: CreateTicketTypeRequest): Observable<TicketTypeDto> {
    return this.http.post<TicketTypeDto>(this.base, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  update(id: number, req: UpdateTicketTypeRequest): Observable<TicketTypeDto> {
    return this.http.put<TicketTypeDto>(`${this.base}/${id}`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 7: Implement `venue.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { VenueDto, CreateVenueRequest, UpdateVenueRequest } from '../models/venue.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class VenueService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Venue`;

  list(): Observable<VenueDto[]> {
    return this.http.get<VenueDto[]>(this.base).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getById(id: number): Observable<VenueDto> {
    return this.http.get<VenueDto>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  create(req: CreateVenueRequest): Observable<VenueDto> {
    return this.http.post<VenueDto>(this.base, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  update(id: number, req: UpdateVenueRequest): Observable<VenueDto> {
    return this.http.put<VenueDto>(`${this.base}/${id}`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 8: Implement `user.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import {
  User, UpdateUserRequest, ChangePasswordRequest, ChangeEmailRequest,
  CloseAccountRequest, UserSearchRequest,
} from '../models/user.model';
import { OrganizerRequestDto } from '../models/admin.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/User`;

  getMe(): Observable<User> {
    return this.http.get<User>(`${this.base}/me`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  updateMe(req: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.base}/me`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  changePassword(req: ChangePasswordRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/me/password`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  changeEmail(req: ChangeEmailRequest): Observable<User> {
    return this.http.put<User>(`${this.base}/me/email`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  deleteMe(req: CloseAccountRequest): Observable<void> {
    return this.http.delete<void>(`${this.base}/me`, { body: req }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  search(req: UserSearchRequest): Observable<PagedResult<User>> {
    return this.http.get<PagedResult<User>>(this.base, { params: toHttpParams({ ...req }) }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getById(id: number): Observable<User> {
    return this.http.get<User>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  deleteById(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
  requestOrganizer(reason: string): Observable<OrganizerRequestDto> {
    return this.http.post<OrganizerRequestDto>(`${this.base}/request-organizer`, { reason }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getOrganizerRequest(): Observable<OrganizerRequestDto> {
    return this.http.get<OrganizerRequestDto>(`${this.base}/organizer-request`).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 9: Implement `admin.service.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import { EventDto } from '../models/event.model';
import { User, UserSearchRequest } from '../models/user.model';
import {
  OrganizerRequestDto, ReviewRequest, OrganizerRequestQueryRequest,
} from '../models/admin.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Admin`;

  getOrganizerRequests(req: OrganizerRequestQueryRequest): Observable<PagedResult<OrganizerRequestDto>> {
    return this.http.get<PagedResult<OrganizerRequestDto>>(`${this.base}/organizer-requests`, { params: toHttpParams({ ...req }) }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  approveOrganizerRequest(id: number, req: ReviewRequest): Observable<OrganizerRequestDto> {
    return this.http.post<OrganizerRequestDto>(`${this.base}/organizer-requests/${id}/approve`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  rejectOrganizerRequest(id: number, req: ReviewRequest): Observable<OrganizerRequestDto> {
    return this.http.post<OrganizerRequestDto>(`${this.base}/organizer-requests/${id}/reject`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getPendingEvents(page: number, pageSize: number): Observable<PagedResult<EventDto>> {
    return this.http.get<PagedResult<EventDto>>(`${this.base}/events/pending`, { params: toHttpParams({ page, pageSize }) }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  approveEvent(id: number, req: ReviewRequest): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/events/${id}/approve`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  rejectEvent(id: number, req: ReviewRequest): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/events/${id}/reject`, req).pipe(catchError(e => throwError(() => extractError(e))));
  }
  getUsers(req: UserSearchRequest): Observable<PagedResult<User>> {
    return this.http.get<PagedResult<User>>(`${this.base}/users`, { params: toHttpParams({ ...req }) }).pipe(catchError(e => throwError(() => extractError(e))));
  }
  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/users/${id}`).pipe(catchError(e => throwError(() => extractError(e))));
  }
}
```

- [ ] **Step 10: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/domain-services.spec.ts'`
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add EMSAngular/src/app/core/services
git commit -m "add domain services"
```

---

### Task 7: SeatHubService (SignalR)

**Files:**
- Create: `EMSAngular/src/app/core/services/seat-hub.service.ts`
- Test: `EMSAngular/src/app/core/services/seat-hub.service.spec.ts`

**Interfaces:**
- Consumes: `environment.apiBaseUrl`; `@microsoft/signalr` `HubConnectionBuilder`. Server emits `SeatReserved`, `SeatReleased`, `SeatBooked` (payload `seatId: number`); client invokes `JoinEventRoom`/`LeaveEventRoom`.
- Produces:
  - `lastUpdate: Signal<{ seatId: number; status: 'reserved' | 'released' | 'booked' } | null>`
  - `joinEvent(eventId: number): Promise<void>`
  - `leaveEvent(eventId: number): Promise<void>`
  - `connect(): Promise<void>` (idempotent — builds and starts the connection once)
  - `disconnect(): Promise<void>`

- [ ] **Step 1: Write the failing test** — `seat-hub.service.spec.ts`

This test verifies signal updates without a live socket by invoking the private handler indirectly through a injected fake connection. Implementation exposes `handleSeatEvent(status, seatId)` for testability.

```ts
import { TestBed } from '@angular/core/testing';
import { SeatHubService } from './seat-hub.service';

describe('SeatHubService', () => {
  let service: SeatHubService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeatHubService] });
    service = TestBed.inject(SeatHubService);
  });

  it('starts with no update', () => {
    expect(service.lastUpdate()).toBeNull();
  });

  it('handleSeatEvent updates the signal', () => {
    service.handleSeatEvent('reserved', 42);
    expect(service.lastUpdate()).toEqual({ seatId: 42, status: 'reserved' });
    service.handleSeatEvent('booked', 7);
    expect(service.lastUpdate()).toEqual({ seatId: 7, status: 'booked' });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/seat-hub.service.spec.ts'`
Expected: FAIL — `SeatHubService` not found.

- [ ] **Step 3: Implement `seat-hub.service.ts`**

```ts
import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

export type SeatStatus = 'reserved' | 'released' | 'booked';
export interface SeatUpdate { seatId: number; status: SeatStatus; }

@Injectable({ providedIn: 'root' })
export class SeatHubService {
  private connection?: HubConnection;
  private updateSignal = signal<SeatUpdate | null>(null);
  readonly lastUpdate = this.updateSignal.asReadonly();

  handleSeatEvent(status: SeatStatus, seatId: number): void {
    this.updateSignal.set({ seatId, status });
  }

  async connect(): Promise<void> {
    if (this.connection) return;
    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/seats`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('SeatReserved', (seatId: number) => this.handleSeatEvent('reserved', seatId));
    this.connection.on('SeatReleased', (seatId: number) => this.handleSeatEvent('released', seatId));
    this.connection.on('SeatBooked', (seatId: number) => this.handleSeatEvent('booked', seatId));

    await this.connection.start();
  }

  async joinEvent(eventId: number): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinEventRoom', eventId);
  }

  async leaveEvent(eventId: number): Promise<void> {
    if (!this.connection) return;
    await this.connection.invoke('LeaveEventRoom', eventId);
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = undefined;
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/seat-hub.service.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/core/services/seat-hub.service.ts EMSAngular/src/app/core/services/seat-hub.service.spec.ts
git commit -m "add seat hub service"
```

---

## Phase 3 — Shared Pipes & Components

### Task 8: IstDatePipe and CurrencyInrPipe

**Files:**
- Create: `EMSAngular/src/app/shared/pipes/ist-date.pipe.ts`
- Create: `EMSAngular/src/app/shared/pipes/currency-inr.pipe.ts`
- Test: `EMSAngular/src/app/shared/pipes/pipes.spec.ts`

**Interfaces:**
- Produces:
  - `IstDatePipe` (`name: 'istDate'`, standalone) — `transform(value: string | Date | null, mode: 'date' | 'datetime' = 'datetime'): string`. The API already returns IST wall-clock times; the pipe formats them WITHOUT applying any timezone offset (treats the string as-is).
  - `CurrencyInrPipe` (`name: 'inr'`, standalone) — `transform(value: number | null): string` → `₹1,200.00`.

- [ ] **Step 1: Write the failing test** — `pipes.spec.ts`

```ts
import { IstDatePipe } from './ist-date.pipe';
import { CurrencyInrPipe } from './currency-inr.pipe';

describe('IstDatePipe', () => {
  const pipe = new IstDatePipe();

  it('returns empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('formats a datetime without shifting the clock', () => {
    // 14:30 in the API string must still read 02:30 PM, not be offset
    const out = pipe.transform('2026-06-19T14:30:00', 'datetime');
    expect(out).toContain('2026');
    expect(out).toMatch(/2:30/);
  });

  it('date mode omits time', () => {
    const out = pipe.transform('2026-06-19T14:30:00', 'date');
    expect(out).not.toMatch(/2:30/);
  });
});

describe('CurrencyInrPipe', () => {
  const pipe = new CurrencyInrPipe();

  it('formats with rupee symbol and two decimals', () => {
    expect(pipe.transform(1200)).toBe('₹1,200.00');
  });

  it('returns ₹0.00 for null', () => {
    expect(pipe.transform(null)).toBe('₹0.00');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/pipes.spec.ts'`
Expected: FAIL — pipes not found.

- [ ] **Step 3: Implement `ist-date.pipe.ts`**

```ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'istDate', standalone: true })
export class IstDatePipe implements PipeTransform {
  transform(value: string | Date | null, mode: 'date' | 'datetime' = 'datetime'): string {
    if (!value) return '';
    // The backend already emits IST wall-clock times. Parse the components
    // directly so we format exactly what was sent, with no timezone shift.
    const iso = typeof value === 'string' ? value : value.toISOString();
    const m = iso.match(/(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/);
    if (!m) return iso;
    const [, y, mo, d, h, min] = m;
    const date = new Date(+y, +mo - 1, +d, +h, +min);
    const dateStr = date.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
    if (mode === 'date') return dateStr;
    const timeStr = date.toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit', hour12: true });
    return `${dateStr}, ${timeStr}`;
  }
}
```

- [ ] **Step 4: Implement `currency-inr.pipe.ts`**

```ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'inr', standalone: true })
export class CurrencyInrPipe implements PipeTransform {
  transform(value: number | null): string {
    const amount = value ?? 0;
    return `₹${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/pipes.spec.ts'`
Expected: PASS. (If the locale time format renders `2:30 pm` lowercase, the regex `/2:30/` still matches.)

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/shared/pipes
git commit -m "add shared pipes"
```

---

### Task 9: Presentational shared components (Alert, LoadingSpinner, Pagination, BookingQr)

These four are pure presentational standalone components. Test the two with logic (Pagination page math, Alert dismiss).

**Files:**
- Create: `EMSAngular/src/app/shared/components/alert/alert.component.ts`
- Create: `EMSAngular/src/app/shared/components/loading-spinner/loading-spinner.component.ts`
- Create: `EMSAngular/src/app/shared/components/pagination/pagination.component.ts`
- Create: `EMSAngular/src/app/shared/components/booking-qr/booking-qr.component.ts`
- Test: `EMSAngular/src/app/shared/components/pagination/pagination.component.spec.ts`

**Interfaces:**
- `AlertComponent` (`ems-alert`): inputs `type: 'success'|'error'|'info'` (default `'info'`), `message: string`; output `dismissed`. Renders only when `message` is truthy.
- `LoadingSpinnerComponent` (`ems-loading-spinner`): no inputs; centered spinner.
- `PaginationComponent` (`ems-pagination`): inputs `currentPage: number`, `totalPages: number`; output `pageChange: EventEmitter<number>`. Exposes `protected pages(): number[]`. Prev disabled at page 1; next disabled at last page.
- `BookingQrComponent` (`ems-booking-qr`): input `qrCode: string` (base64 PNG, may or may not include the data-URI prefix); renders `<img>` + download link. Exposes `protected src(): string` that prepends `data:image/png;base64,` when missing.

- [ ] **Step 1: Write the failing test** — `pagination.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  let fixture: ComponentFixture<PaginationComponent>;
  let component: PaginationComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PaginationComponent] });
    fixture = TestBed.createComponent(PaginationComponent);
    component = fixture.componentInstance;
  });

  it('emits the next page when goTo is called', () => {
    let emitted = -1;
    component.pageChange.subscribe((p: number) => (emitted = p));
    fixture.componentRef.setInput('currentPage', 2);
    fixture.componentRef.setInput('totalPages', 5);
    component.goTo(3);
    expect(emitted).toBe(3);
  });

  it('does not emit for out-of-range pages', () => {
    let called = false;
    component.pageChange.subscribe(() => (called = true));
    fixture.componentRef.setInput('currentPage', 1);
    fixture.componentRef.setInput('totalPages', 3);
    component.goTo(0);
    component.goTo(4);
    expect(called).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/pagination.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `pagination.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-pagination',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="flex items-center justify-center gap-1 py-4" *ngIf="totalPages > 1">
      <button class="px-3 py-1 rounded-lg border border-gray-300 text-gray-700 disabled:opacity-40"
              [disabled]="currentPage <= 1" (click)="goTo(currentPage - 1)">Prev</button>
      <button *ngFor="let p of pages()"
              class="px-3 py-1 rounded-lg border"
              [class.bg-indigo-600]="p === currentPage"
              [class.text-white]="p === currentPage"
              [class.border-indigo-600]="p === currentPage"
              [class.border-gray-300]="p !== currentPage"
              (click)="goTo(p)">{{ p }}</button>
      <button class="px-3 py-1 rounded-lg border border-gray-300 text-gray-700 disabled:opacity-40"
              [disabled]="currentPage >= totalPages" (click)="goTo(currentPage + 1)">Next</button>
    </nav>
  `,
})
export class PaginationComponent {
  @Input() currentPage = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();

  protected pages(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goTo(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) return;
    this.pageChange.emit(page);
  }
}
```

- [ ] **Step 4: Implement `alert.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-alert',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="message" class="flex items-start justify-between gap-3 rounded-lg px-4 py-3 text-sm"
         [class.bg-green-50]="type === 'success'" [class.text-green-700]="type === 'success'"
         [class.bg-red-50]="type === 'error'" [class.text-red-700]="type === 'error'"
         [class.bg-indigo-50]="type === 'info'" [class.text-indigo-700]="type === 'info'">
      <span>{{ message }}</span>
      <button class="font-bold opacity-60 hover:opacity-100" (click)="dismissed.emit()">×</button>
    </div>
  `,
})
export class AlertComponent {
  @Input() type: 'success' | 'error' | 'info' = 'info';
  @Input() message = '';
  @Output() dismissed = new EventEmitter<void>();
}
```

- [ ] **Step 5: Implement `loading-spinner.component.ts`**

```ts
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ems-loading-spinner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex justify-center py-10">
      <div class="h-8 w-8 animate-spin rounded-full border-4 border-gray-200 border-t-indigo-600"></div>
    </div>
  `,
})
export class LoadingSpinnerComponent {}
```

- [ ] **Step 6: Implement `booking-qr.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'ems-booking-qr',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center gap-2">
      <img [src]="src()" alt="Booking QR code" class="h-48 w-48 rounded-lg border border-gray-200" />
      <a [href]="src()" download="ticket-qr.png"
         class="text-sm text-indigo-600 hover:underline">Download QR</a>
    </div>
  `,
})
export class BookingQrComponent {
  @Input() qrCode = '';

  protected src(): string {
    if (!this.qrCode) return '';
    return this.qrCode.startsWith('data:') ? this.qrCode : `data:image/png;base64,${this.qrCode}`;
  }
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/pagination.component.spec.ts'`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add EMSAngular/src/app/shared/components/alert EMSAngular/src/app/shared/components/loading-spinner EMSAngular/src/app/shared/components/pagination EMSAngular/src/app/shared/components/booking-qr
git commit -m "add presentational components"
```

---

### Task 10: EventCardComponent

**Files:**
- Create: `EMSAngular/src/app/shared/components/event-card/event-card.component.ts`
- Test: `EMSAngular/src/app/shared/components/event-card/event-card.component.spec.ts`

**Interfaces:**
- `EventCardComponent` (`ems-event-card`): input `event: EventDto` (required). Renders image, title, `istDate` of `startTime`, category badge. The whole card is a `routerLink` to `/events/{{event.slug}}`. Uses `IstDatePipe`, `RouterLink`, `CommonModule`.

- [ ] **Step 1: Write the failing test** — `event-card.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EventCardComponent } from './event-card.component';
import { EventDto } from '../../../core/models/event.model';

const ev: EventDto = {
  id: 1, organizerId: 1, venueId: 1, title: 'Rock Night',
  description: 'desc', status: 'Published', startTime: '2026-07-01T19:00:00',
  endTime: '2026-07-01T22:00:00', imageUrl: 'http://img/x.jpg',
  category: 'Music', slug: 'rock-night', createdAt: '2026-06-01T00:00:00',
};

describe('EventCardComponent', () => {
  let fixture: ComponentFixture<EventCardComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [EventCardComponent], providers: [provideRouter([])] });
    fixture = TestBed.createComponent(EventCardComponent);
    fixture.componentRef.setInput('event', ev);
    fixture.detectChanges();
  });

  it('renders the title and category', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Rock Night');
    expect(text).toContain('Music');
  });

  it('links to the event slug', () => {
    const anchor = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(anchor?.getAttribute('href')).toContain('/events/rock-night');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-card.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `event-card.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EventDto } from '../../../core/models/event.model';
import { IstDatePipe } from '../../pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-card',
  standalone: true,
  imports: [CommonModule, RouterLink, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a [routerLink]="['/events', event.slug]"
       class="block overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm transition hover:shadow-md">
      <img [src]="event.imageUrl" [alt]="event.title" class="aspect-video w-full object-cover" />
      <div class="space-y-1 p-4">
        <span class="inline-block rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700">
          {{ event.category }}
        </span>
        <h3 class="font-semibold text-gray-900">{{ event.title }}</h3>
        <p class="text-sm text-gray-600">{{ event.startTime | istDate }}</p>
      </div>
    </a>
  `,
})
export class EventCardComponent {
  @Input({ required: true }) event!: EventDto;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-card.component.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/shared/components/event-card
git commit -m "add event card component"
```

---

### Task 11: SeatMapComponent

**Files:**
- Create: `EMSAngular/src/app/shared/components/seat-map/seat-map.component.ts`
- Test: `EMSAngular/src/app/shared/components/seat-map/seat-map.component.spec.ts`

**Interfaces:**
- Consumes: `SeatService.getAvailableByEvent()`, `SeatHubService` (`lastUpdate` signal, `joinEvent`, `leaveEvent`). `SeatDto`.
- Produces: `SeatMapComponent` (`ems-seat-map`):
  - inputs: `eventId: number` (required), `venueId: number` (required), `selectedSeatIds: number[]` (default `[]`)
  - output: `seatToggled: EventEmitter<SeatDto>` — emitted when a user clicks an available (or already-selected) seat.
  - internal `availableIds = signal<Set<number>>` initialised from `getAvailableByEvent`; an `effect()` watching `SeatHubService.lastUpdate` removes the seat id on `reserved`/`booked` and adds it back on `released`.
  - exposes `protected seatState(seat): 'selected' | 'available' | 'taken'` and `protected onSeatClick(seat)`.
  - groups seats by section then row for rendering.

- [ ] **Step 1: Write the failing test** — `seat-map.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { SeatMapComponent } from './seat-map.component';
import { SeatService } from '../../../core/services/seat.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';
import { SeatDto } from '../../../core/models/seat.model';

const seats: SeatDto[] = [
  { id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' },
  { id: 2, venueId: 1, section: 'A', row: '1', seatNumber: 2, seatType: 'VIP' },
];

describe('SeatMapComponent', () => {
  let fixture: ComponentFixture<SeatMapComponent>;
  let component: SeatMapComponent;
  let hub: { lastUpdate: any; joinEvent: jasmine.Spy; leaveEvent: jasmine.Spy };

  beforeEach(() => {
    hub = { lastUpdate: signal(null), joinEvent: jasmine.createSpy().and.resolveTo(), leaveEvent: jasmine.createSpy().and.resolveTo() };
    TestBed.configureTestingModule({
      imports: [SeatMapComponent],
      providers: [
        { provide: SeatService, useValue: { getAvailableByEvent: () => of(seats) } },
        { provide: SeatHubService, useValue: hub },
      ],
    });
    fixture = TestBed.createComponent(SeatMapComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('eventId', 10);
    fixture.componentRef.setInput('venueId', 1);
    fixture.detectChanges();
  });

  it('marks fetched seats available and joins the event room', () => {
    expect(component['seatState'](seats[0])).toBe('available');
    expect(hub.joinEvent).toHaveBeenCalledWith(10);
  });

  it('emits when an available seat is clicked', () => {
    let emitted: SeatDto | null = null;
    component.seatToggled.subscribe((s: SeatDto) => (emitted = s));
    component['onSeatClick'](seats[0]);
    expect(emitted).toEqual(seats[0]);
  });

  it('marks a seat taken after a SeatBooked hub event', () => {
    hub.lastUpdate.set({ seatId: 1, status: 'booked' });
    fixture.detectChanges();
    expect(component['seatState'](seats[0])).toBe('taken');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/seat-map.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `seat-map.component.ts`**

```ts
import {
  ChangeDetectionStrategy, Component, EventEmitter, Input, OnDestroy, OnInit,
  Output, computed, effect, inject, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeatService } from '../../../core/services/seat.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';
import { SeatDto } from '../../../core/models/seat.model';

interface SeatRow { row: string; seats: SeatDto[]; }
interface SeatSection { section: string; rows: SeatRow[]; }

@Component({
  selector: 'ems-seat-map',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-x-auto">
      <div class="mb-4 flex flex-wrap gap-4 text-xs text-gray-600">
        <span class="flex items-center gap-1"><i class="h-3 w-3 rounded border border-indigo-600 bg-white"></i> Available</span>
        <span class="flex items-center gap-1"><i class="h-3 w-3 rounded bg-indigo-600"></i> Selected</span>
        <span class="flex items-center gap-1"><i class="h-3 w-3 rounded bg-gray-400"></i> Taken</span>
      </div>
      <div class="space-y-6">
        <div *ngFor="let section of sections()">
          <h4 class="mb-2 text-sm font-semibold text-gray-700">Section {{ section.section }}</h4>
          <div class="space-y-1">
            <div *ngFor="let r of section.rows" class="flex items-center gap-1">
              <span class="w-6 text-xs text-gray-400">{{ r.row }}</span>
              <button *ngFor="let seat of r.seats" type="button"
                      class="h-8 w-8 rounded border text-xs"
                      [class.border-indigo-600]="seatState(seat) === 'available'"
                      [class.bg-white]="seatState(seat) === 'available'"
                      [class.bg-indigo-600]="seatState(seat) === 'selected'"
                      [class.text-white]="seatState(seat) === 'selected'"
                      [class.bg-gray-400]="seatState(seat) === 'taken'"
                      [class.cursor-not-allowed]="seatState(seat) === 'taken'"
                      [disabled]="seatState(seat) === 'taken'"
                      (click)="onSeatClick(seat)">{{ seat.seatNumber }}</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class SeatMapComponent implements OnInit, OnDestroy {
  private seatService = inject(SeatService);
  private hub = inject(SeatHubService);

  @Input({ required: true }) eventId!: number;
  @Input({ required: true }) venueId!: number;
  @Input() selectedSeatIds: number[] = [];
  @Output() seatToggled = new EventEmitter<SeatDto>();

  private allSeats = signal<SeatDto[]>([]);
  private availableIds = signal<Set<number>>(new Set());
  protected sections = computed<SeatSection[]>(() => this.group(this.allSeats()));

  constructor() {
    effect(() => {
      const update = this.hub.lastUpdate();
      if (!update) return;
      this.availableIds.update(set => {
        const next = new Set(set);
        if (update.status === 'released') next.add(update.seatId);
        else next.delete(update.seatId);
        return next;
      });
    });
  }

  ngOnInit(): void {
    this.seatService.getAvailableByEvent(this.eventId).subscribe(seats => {
      this.allSeats.set(seats);
      this.availableIds.set(new Set(seats.map(s => s.id)));
    });
    void this.hub.joinEvent(this.eventId);
  }

  ngOnDestroy(): void {
    void this.hub.leaveEvent(this.eventId);
  }

  protected seatState(seat: SeatDto): 'selected' | 'available' | 'taken' {
    if (this.selectedSeatIds.includes(seat.id)) return 'selected';
    return this.availableIds().has(seat.id) ? 'available' : 'taken';
  }

  protected onSeatClick(seat: SeatDto): void {
    if (this.seatState(seat) === 'taken') return;
    this.seatToggled.emit(seat);
  }

  private group(seats: SeatDto[]): SeatSection[] {
    const bySection = new Map<string, Map<string, SeatDto[]>>();
    for (const s of seats) {
      if (!bySection.has(s.section)) bySection.set(s.section, new Map());
      const rows = bySection.get(s.section)!;
      if (!rows.has(s.row)) rows.set(s.row, []);
      rows.get(s.row)!.push(s);
    }
    return [...bySection.entries()].map(([section, rows]) => ({
      section,
      rows: [...rows.entries()].map(([row, rowSeats]) => ({
        row,
        seats: rowSeats.sort((a, b) => a.seatNumber - b.seatNumber),
      })),
    }));
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/seat-map.component.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/shared/components/seat-map
git commit -m "add seat map component"
```

---

### Task 12: StripePaymentComponent

**Files:**
- Create: `EMSAngular/src/app/shared/components/stripe-payment/stripe-payment.component.ts`
- Test: `EMSAngular/src/app/shared/components/stripe-payment/stripe-payment.component.spec.ts`

**Interfaces:**
- Consumes: `loadStripe` from `@stripe/stripe-js`; `environment.stripePublishableKey`.
- Produces: `StripePaymentComponent` (`ems-stripe-payment`):
  - input: `clientSecret: string` (required)
  - outputs: `paymentSucceeded: EventEmitter<string>` (emits the `paymentIntentId`), `paymentFailed: EventEmitter<string>` (emits an error message)
  - `protected submitting = signal(false)`, `protected errorMessage = signal('')`
  - `protected pay()` calls `stripe.confirmPayment({ elements, redirect: 'if_required' })`; on success emits `paymentSucceeded` with `paymentIntent.id`; on error sets `errorMessage` and emits `paymentFailed`.
  - The Stripe `Elements` + `PaymentElement` are created in `ngAfterViewInit` and mounted into `#payment-element`. The injectable `loadStripe` is wrapped in a `protected loadStripeFn = loadStripe` property so the test can override it.

- [ ] **Step 1: Write the failing test** — `stripe-payment.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StripePaymentComponent } from './stripe-payment.component';

describe('StripePaymentComponent', () => {
  let fixture: ComponentFixture<StripePaymentComponent>;
  let component: StripePaymentComponent;

  const stripeStub = {
    elements: () => ({ create: () => ({ mount: () => {} }) }),
    confirmPayment: jasmine.createSpy().and.resolveTo({ paymentIntent: { id: 'pi_123', status: 'succeeded' } }),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [StripePaymentComponent] });
    fixture = TestBed.createComponent(StripePaymentComponent);
    component = fixture.componentInstance;
    component['loadStripeFn'] = (() => Promise.resolve(stripeStub)) as any;
    fixture.componentRef.setInput('clientSecret', 'cs_test_123');
  });

  it('emits paymentSucceeded with the intent id on success', async () => {
    let emitted = '';
    component.paymentSucceeded.subscribe((id: string) => (emitted = id));
    await fixture.whenStable();
    component['stripe'] = stripeStub as any;
    component['elements'] = stripeStub.elements() as any;
    await component['pay']();
    expect(emitted).toBe('pi_123');
  });

  it('emits paymentFailed when confirmPayment returns an error', async () => {
    let failed = '';
    component.paymentFailed.subscribe((m: string) => (failed = m));
    component['stripe'] = { confirmPayment: () => Promise.resolve({ error: { message: 'Card declined' } }) } as any;
    component['elements'] = {} as any;
    await component['pay']();
    expect(failed).toBe('Card declined');
    expect(component['errorMessage']()).toBe('Card declined');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/stripe-payment.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `stripe-payment.component.ts`**

```ts
import {
  AfterViewInit, ChangeDetectionStrategy, Component, EventEmitter, Input, Output, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { loadStripe, Stripe, StripeElements } from '@stripe/stripe-js';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'ems-stripe-payment',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form (ngSubmit)="pay()" class="space-y-4">
      <div id="payment-element"></div>
      <p *ngIf="errorMessage()" class="text-sm text-red-600">{{ errorMessage() }}</p>
      <button type="submit" [disabled]="submitting()"
              class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">
        {{ submitting() ? 'Processing…' : 'Pay now' }}
      </button>
    </form>
  `,
})
export class StripePaymentComponent implements AfterViewInit {
  @Input({ required: true }) clientSecret!: string;
  @Output() paymentSucceeded = new EventEmitter<string>();
  @Output() paymentFailed = new EventEmitter<string>();

  protected submitting = signal(false);
  protected errorMessage = signal('');
  protected loadStripeFn = loadStripe;

  private stripe: Stripe | null = null;
  private elements: StripeElements | null = null;

  async ngAfterViewInit(): Promise<void> {
    this.stripe = await this.loadStripeFn(environment.stripePublishableKey);
    if (!this.stripe) { this.errorMessage.set('Failed to load payment provider.'); return; }
    this.elements = this.stripe.elements({ clientSecret: this.clientSecret });
    const paymentElement = this.elements.create('payment');
    paymentElement.mount('#payment-element');
  }

  protected async pay(): Promise<void> {
    if (!this.stripe || !this.elements) return;
    this.submitting.set(true);
    this.errorMessage.set('');
    const result = await this.stripe.confirmPayment({
      elements: this.elements,
      redirect: 'if_required',
    });
    this.submitting.set(false);
    if (result.error) {
      const msg = result.error.message ?? 'Payment failed.';
      this.errorMessage.set(msg);
      this.paymentFailed.emit(msg);
      return;
    }
    this.paymentSucceeded.emit(result.paymentIntent!.id);
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/stripe-payment.component.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/shared/components/stripe-payment
git commit -m "add stripe payment component"
```

---

## Phase 4 — App Shell, Navbar, Routing

### Task 13: NavbarComponent, app bootstrap with interceptors, and root routing

**Files:**
- Create: `EMSAngular/src/app/shared/components/navbar/navbar.component.ts`
- Test: `EMSAngular/src/app/shared/components/navbar/navbar.component.spec.ts`
- Modify: `EMSAngular/src/app/app.component.ts`
- Modify: `EMSAngular/src/app/app.config.ts` (standalone bootstrap) OR `app.module.ts` if module-based — this plan assumes the default standalone bootstrap from `ng new`.
- Modify: `EMSAngular/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `AuthService` (`isAuthenticated`, `role`, `currentUser`, `logout`); `jwtInterceptor`, `authErrorInterceptor`.
- Produces:
  - `NavbarComponent` (`ems-navbar`): role-aware links; `protected menuOpen = signal(false)`; `protected logout()` calls `AuthService.logout()` then navigates to `/events`.
  - Root routes wiring all five lazy feature route files + guards.
  - `app.config.ts` registers `provideHttpClient(withInterceptors([jwtInterceptor, authErrorInterceptor]))` and `provideRouter(routes)`.

- [ ] **Step 1: Write the failing test** — `navbar.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { NavbarComponent } from './navbar.component';
import { AuthService } from '../../../core/services/auth.service';

describe('NavbarComponent', () => {
  let fixture: ComponentFixture<NavbarComponent>;
  let auth: any;

  function setup() {
    TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    fixture = TestBed.createComponent(NavbarComponent);
    fixture.detectChanges();
  }

  it('shows login link when unauthenticated', () => {
    auth = { isAuthenticated: signal(false), role: signal(null), currentUser: signal(null), logout: () => {} };
    setup();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Login');
  });

  it('shows Admin link for admin role', () => {
    auth = {
      isAuthenticated: signal(true), role: signal('Admin'),
      currentUser: signal({ name: 'Boss' }), logout: () => {},
    };
    setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Admin');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/navbar.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `navbar.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'ems-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="border-b border-gray-200 bg-white">
      <nav class="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <a routerLink="/events" class="text-lg font-bold text-indigo-600">EventHub</a>

        <button class="sm:hidden" (click)="menuOpen.set(!menuOpen())" aria-label="Toggle menu">☰</button>

        <div class="hidden items-center gap-4 sm:flex" [class.flex]="menuOpen()" [class.hidden]="!menuOpen()">
          <a routerLink="/events" routerLinkActive="text-indigo-600" class="text-sm text-gray-700">Events</a>

          <ng-container *ngIf="auth.isAuthenticated(); else guestLinks">
            <a routerLink="/bookings" class="text-sm text-gray-700">My Bookings</a>
            <a *ngIf="canOrganize()" routerLink="/organizer/events" class="text-sm text-gray-700">My Events</a>
            <a *ngIf="auth.role() === 'Admin'" routerLink="/admin/events" class="text-sm text-gray-700">Admin</a>
            <span class="text-sm text-gray-500">{{ auth.currentUser()?.name }}</span>
            <button (click)="logout()" class="rounded-lg border border-gray-300 px-3 py-1 text-sm text-gray-700 hover:bg-gray-50">Logout</button>
          </ng-container>

          <ng-template #guestLinks>
            <a routerLink="/auth/login" class="text-sm text-gray-700">Login</a>
            <a routerLink="/auth/register" class="rounded-lg bg-indigo-600 px-3 py-1 text-sm text-white hover:bg-indigo-700">Register</a>
          </ng-template>
        </div>
      </nav>
    </header>
  `,
})
export class NavbarComponent {
  protected auth = inject(AuthService);
  private router = inject(Router);

  protected menuOpen = signal(false);
  protected canOrganize = computed(() => this.auth.role() === 'Organizer' || this.auth.role() === 'Admin');

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/events']);
  }
}
```

- [ ] **Step 4: Implement `app.component.ts`**

```ts
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './shared/components/navbar/navbar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent],
  template: `
    <ems-navbar />
    <main class="mx-auto max-w-6xl px-4 py-6">
      <router-outlet />
    </main>
  `,
})
export class AppComponent {}
```

- [ ] **Step 5: Implement `app.config.ts`**

```ts
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { jwtInterceptor } from './core/interceptors/jwt.interceptor';
import { authErrorInterceptor } from './core/interceptors/auth-error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([jwtInterceptor, authErrorInterceptor])),
  ],
};
```

- [ ] **Step 6: Implement `app.routes.ts`** (feature route files are created in later tasks; the `loadChildren` targets must match the filenames used there)

```ts
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'events' },
  {
    path: 'events',
    loadChildren: () => import('./features/events/events.routes').then(m => m.EVENTS_ROUTES),
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES),
  },
  {
    path: 'bookings',
    canActivate: [authGuard],
    loadChildren: () => import('./features/bookings/bookings.routes').then(m => m.BOOKINGS_ROUTES),
  },
  {
    path: 'checkout',
    canActivate: [authGuard],
    loadChildren: () => import('./features/events/checkout.routes').then(m => m.CHECKOUT_ROUTES),
  },
  {
    path: 'organizer',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Organizer', 'Admin'] },
    loadChildren: () => import('./features/organizer/organizer.routes').then(m => m.ORGANIZER_ROUTES),
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
    loadChildren: () => import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
  },
  { path: '**', redirectTo: 'events' },
];
```

> NOTE: This plan uses **standalone route files** (`*.routes.ts` exporting a `Routes` const) rather than NgModules. Although the spec named NgModules, Angular's current lazy-loading uses `loadChildren` with route arrays, which is functionally the lazy-loaded "feature module" boundary and matches the `ng new` standalone scaffold. Each feature folder owns its own route file. If you must use NgModules, wrap each route file in a module — but standalone is recommended and assumed here.

- [ ] **Step 7: Run navbar test + full build**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/navbar.component.spec.ts'`
Expected: PASS.
(The full `ng build` will fail until the feature route files exist — that is expected and resolved in Tasks 14–20. Do not run `ng build` at this step.)

- [ ] **Step 8: Commit**

```bash
git add EMSAngular/src/app/shared/components/navbar EMSAngular/src/app/app.component.ts EMSAngular/src/app/app.config.ts EMSAngular/src/app/app.routes.ts
git commit -m "add navbar and routing"
```

---

## Phase 5 — Auth Feature

### Task 14: Login, Register, Forgot/Reset Password + auth routes

**Files:**
- Create: `EMSAngular/src/app/features/auth/login/login.component.ts`
- Create: `EMSAngular/src/app/features/auth/register/register.component.ts`
- Create: `EMSAngular/src/app/features/auth/forgot-password/forgot-password.component.ts`
- Create: `EMSAngular/src/app/features/auth/reset-password/reset-password.component.ts`
- Create: `EMSAngular/src/app/features/auth/auth.routes.ts`
- Test: `EMSAngular/src/app/features/auth/login/login.component.spec.ts`

**Interfaces:**
- Consumes: `AuthService.login/register/forgotPassword/resetPassword`; `AlertComponent`; `ReactiveFormsModule`.
- Produces: `AUTH_ROUTES: Routes` with paths `login`, `register`, `forgot-password`, `reset-password`. `LoginComponent` navigates to `returnUrl` query param (or `/events`) on success.

- [ ] **Step 1: Write the failing test** — `login.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/services/auth.service';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let auth: { login: jasmine.Spy };
  let router: Router;

  beforeEach(() => {
    auth = { login: jasmine.createSpy() };
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
    const navSpy = spyOn(router, 'navigateByUrl');
    auth.login.and.returnValue(of({}));
    component.form.setValue({ email: 'a@b.com', password: 'secret12' });
    component.submit();
    expect(auth.login).toHaveBeenCalled();
    expect(navSpy).toHaveBeenCalled();
  });

  it('shows an error message on failed login', () => {
    auth.login.and.returnValue(throwError(() => 'Invalid credentials'));
    component.form.setValue({ email: 'a@b.com', password: 'secret12' });
    component.submit();
    expect(component.error()).toBe('Invalid credentials');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/login.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `login.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Sign in</h1>
      <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="email" type="email" placeholder="Email"
               class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:ring-2 focus:ring-indigo-500" />
        <input formControlName="password" type="password" placeholder="Password"
               class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:ring-2 focus:ring-indigo-500" />
        <button type="submit" [disabled]="submitting()"
                class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">Sign in</button>
      </form>
      <div class="mt-3 flex justify-between text-sm">
        <a routerLink="/auth/register" class="text-indigo-600 hover:underline">Create account</a>
        <a routerLink="/auth/forgot-password" class="text-indigo-600 hover:underline">Forgot password?</a>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected submitting = signal(false);
  protected error = signal('');
  protected form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.error.set('');
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/events';
        this.router.navigateByUrl(returnUrl);
      },
      error: (msg: string) => { this.error.set(msg); this.submitting.set(false); },
    });
  }
}
```

- [ ] **Step 4: Implement `register.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Create account</h1>
      <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="name" placeholder="Full name" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="email" type="email" placeholder="Email" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="phone" placeholder="Phone" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="password" type="password" placeholder="Password (min 8)" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <button type="submit" [disabled]="submitting()"
                class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">Register</button>
      </form>
      <a routerLink="/auth/login" class="mt-3 block text-sm text-indigo-600 hover:underline">Already have an account?</a>
    </div>
  `,
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  protected submitting = signal(false);
  protected error = signal('');
  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.pattern(/^\+?[0-9]{7,15}$/)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.error.set('');
    this.auth.register({ ...this.form.getRawValue(), role: 'User' }).subscribe({
      next: () => this.router.navigateByUrl('/events'),
      error: (msg: string) => { this.error.set(msg); this.submitting.set(false); },
    });
  }
}
```

- [ ] **Step 5: Implement `forgot-password.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Reset password</h1>
      <ems-alert [type]="resetToken() ? 'success' : 'error'" [message]="message()" (dismissed)="message.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="email" type="email" placeholder="Email" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <button type="submit" class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Send reset token</button>
      </form>
      <a *ngIf="resetToken()" [routerLink]="['/auth/reset-password']" [queryParams]="{ token: resetToken() }"
         class="mt-3 block text-sm text-indigo-600 hover:underline">Continue to reset →</a>
    </div>
  `,
})
export class ForgotPasswordComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);

  protected message = signal('');
  protected resetToken = signal('');
  protected form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      next: res => { this.message.set(res.message); this.resetToken.set(res.resetToken); },
      error: (msg: string) => this.message.set(msg),
    });
  }
}
```

- [ ] **Step 6: Implement `reset-password.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Set new password</h1>
      <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="token" placeholder="Reset token" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="newPassword" type="password" placeholder="New password (min 8)" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <button type="submit" class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Reset password</button>
      </form>
    </div>
  `,
})
export class ResetPasswordComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected error = signal('');
  protected form = this.fb.nonNullable.group({
    token: [this.route.snapshot.queryParamMap.get('token') ?? '', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.auth.resetPassword(this.form.getRawValue()).subscribe({
      next: () => this.router.navigateByUrl('/auth/login'),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
```

- [ ] **Step 7: Implement `auth.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';

export const AUTH_ROUTES: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
];
```

- [ ] **Step 8: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/login.component.spec.ts'`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add EMSAngular/src/app/features/auth
git commit -m "add auth feature"
```

---

## Phase 6 — Events Feature (Browse, Detail, Checkout)

### Task 15: Event browse list + events.routes

**Files:**
- Create: `EMSAngular/src/app/features/events/event-list/event-list.component.ts`
- Create: `EMSAngular/src/app/features/events/events.routes.ts`
- Test: `EMSAngular/src/app/features/events/event-list/event-list.component.spec.ts`

**Interfaces:**
- Consumes: `EventService.search()`; `EventCardComponent`, `PaginationComponent`, `LoadingSpinnerComponent`, `AlertComponent`.
- Produces: `EVENTS_ROUTES` with `''` → `EventListComponent`, `':slug'` → `EventDetailComponent` (created in Task 16). `EventListComponent` holds `events`, `loading`, `error`, `page`, `totalPages` signals and a search form.

- [ ] **Step 1: Write the failing test** — `event-list.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EventListComponent } from './event-list.component';
import { EventService } from '../../../core/services/event.service';
import { PagedResult } from '../../../core/models/paged-result.model';
import { EventDto } from '../../../core/models/event.model';

const page: PagedResult<EventDto> = {
  items: [{ id: 1, organizerId: 1, venueId: 1, title: 'Jazz', description: '', status: 'Published',
    startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', imageUrl: '', category: 'Music',
    slug: 'jazz', createdAt: '2026-06-01T00:00:00' }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('EventListComponent', () => {
  let fixture: ComponentFixture<EventListComponent>;
  let component: EventListComponent;
  let search: jasmine.Spy;

  beforeEach(() => {
    search = jasmine.createSpy().and.returnValue(of(page));
    TestBed.configureTestingModule({
      imports: [EventListComponent],
      providers: [provideRouter([]), { provide: EventService, useValue: { search } }],
    });
    fixture = TestBed.createComponent(EventListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads events on init', () => {
    expect(search).toHaveBeenCalled();
    expect(component['events']().length).toBe(1);
    expect(component['loading']()).toBe(false);
  });

  it('re-searches when applyFilters is called', () => {
    search.calls.reset();
    component['applyFilters']();
    expect(search).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-list.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `event-list.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { EventService } from '../../../core/services/event.service';
import { EventDto } from '../../../core/models/event.model';
import { EventCardComponent } from '../../../shared/components/event-card/event-card.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-event-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, EventCardComponent, PaginationComponent, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Upcoming Events</h1>
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-6 flex flex-wrap gap-3">
      <input formControlName="query" placeholder="Search events…" class="flex-1 rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="category" placeholder="Category" class="w-40 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Search</button>
    </form>

    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
      <ems-event-card *ngFor="let ev of events()" [event]="ev" />
    </div>
    <p *ngIf="!loading() && events().length === 0" class="py-10 text-center text-gray-500">No events found.</p>

    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class EventListComponent implements OnInit {
  private eventService = inject(EventService);
  private fb = inject(FormBuilder);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);
  protected filters = this.fb.nonNullable.group({ query: '', category: '' });

  ngOnInit(): void { this.load(); }

  protected applyFilters(): void { this.page.set(1); this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    const { query, category } = this.filters.getRawValue();
    this.eventService.search({ query, category, page: this.page(), pageSize: 9 }).subscribe({
      next: res => {
        this.events.set(res.items);
        this.totalPages.set(res.totalPages);
        this.loading.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 4: Implement `events.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { EventListComponent } from './event-list/event-list.component';
import { EventDetailComponent } from './event-detail/event-detail.component';

export const EVENTS_ROUTES: Routes = [
  { path: '', component: EventListComponent },
  { path: ':slug', component: EventDetailComponent },
];
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-list.component.spec.ts'`
Expected: PASS (the `events.routes.ts` import of `EventDetailComponent` requires Task 16; create a temporary empty placeholder if running this task in isolation, or run Tasks 15–17 together before `ng build`).

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/features/events/event-list EMSAngular/src/app/features/events/events.routes.ts
git commit -m "add event list"
```

---

### Task 16: Event detail + seat selection

**Files:**
- Create: `EMSAngular/src/app/features/events/event-detail/event-detail.component.ts`
- Test: `EMSAngular/src/app/features/events/event-detail/event-detail.component.spec.ts`

**Interfaces:**
- Consumes: `EventService.getBySlug()`, `TicketTypeService.getActiveByEvent()`, `SeatService.reserve/releaseReservation`, `BookingService.create()`, `AuthService.isAuthenticated()`; `SeatMapComponent`, `IstDatePipe`, `CurrencyInrPipe`, `AlertComponent`, `LoadingSpinnerComponent`.
- Produces: `EventDetailComponent`. Holds `event`, `ticketTypes`, `loading`, `error`, `selected = signal<SeatReservationDto[]>([])`. The user picks a ticket type (sets `activeTicketTypeId`), then clicks seats on the map. `onSeatToggled(seat)` reserves (or releases) and updates `selected`. `checkout()` builds `CreateBookingRequest` and navigates to `/checkout/:bookingId`.

- [ ] **Step 1: Write the failing test** — `event-detail.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { EventDetailComponent } from './event-detail.component';
import { EventService } from '../../../core/services/event.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { SeatService } from '../../../core/services/seat.service';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';
import { signal } from '@angular/core';

const ev = { id: 5, organizerId: 1, venueId: 2, title: 'Show', description: 'd', status: 'Published',
  startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', imageUrl: '', category: 'Music',
  slug: 'show', createdAt: '2026-06-01T00:00:00' };
const tt = { id: 9, eventId: 5, name: 'VIP', seatType: 'VIP', price: 100, totalQuantity: 50,
  availableQuantity: 50, saleStart: '2026-06-01T00:00:00', saleEnd: '2026-06-30T00:00:00', isActive: true, createdAt: '' };
const reservation = { id: 77, seatId: 1, eventId: 5, ticketTypeId: 9, userId: 1, status: 'Active', reservedUntil: '', createdAt: '' };

describe('EventDetailComponent', () => {
  let fixture: ComponentFixture<EventDetailComponent>;
  let component: EventDetailComponent;
  let booking: { create: jasmine.Spy };

  beforeEach(() => {
    booking = { create: jasmine.createSpy().and.returnValue(of({ id: 123 })) };
    TestBed.configureTestingModule({
      imports: [EventDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'show' } } } },
        { provide: EventService, useValue: { getBySlug: () => of(ev) } },
        { provide: TicketTypeService, useValue: { getActiveByEvent: () => of([tt]) } },
        { provide: SeatService, useValue: { reserve: () => of(reservation), releaseReservation: () => of(void 0) } },
        { provide: BookingService, useValue: booking },
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } },
        { provide: SeatHubService, useValue: { lastUpdate: signal(null), joinEvent: () => Promise.resolve(), leaveEvent: () => Promise.resolve() } },
      ],
    });
    fixture = TestBed.createComponent(EventDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the event and ticket types', () => {
    expect(component['event']()?.title).toBe('Show');
    expect(component['ticketTypes']().length).toBe(1);
  });

  it('reserves a seat and tracks it in selected', () => {
    component['activeTicketTypeId'].set(9);
    component['onSeatToggled']({ id: 1, venueId: 2, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' });
    expect(component['selected']().length).toBe(1);
    expect(component['selected']()[0].id).toBe(77);
  });

  it('checkout creates a booking and navigates', () => {
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate');
    component['activeTicketTypeId'].set(9);
    component['onSeatToggled']({ id: 1, venueId: 2, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' });
    component['checkout']();
    expect(booking.create).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/checkout', 123]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-detail.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `event-detail.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { SeatService } from '../../../core/services/seat.service';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { EventDto } from '../../../core/models/event.model';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { SeatDto, SeatReservationDto } from '../../../core/models/seat.model';
import { SeatMapComponent } from '../../../shared/components/seat-map/seat-map.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-event-detail',
  standalone: true,
  imports: [CommonModule, SeatMapComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <article *ngIf="event() as ev" class="space-y-6">
      <img [src]="ev.imageUrl" [alt]="ev.title" class="aspect-[3/1] w-full rounded-lg object-cover" />
      <header>
        <span class="text-xs font-medium text-indigo-600">{{ ev.category }}</span>
        <h1 class="text-2xl font-semibold text-gray-900">{{ ev.title }}</h1>
        <p class="text-gray-600">{{ ev.startTime | istDate }}</p>
      </header>
      <p class="text-gray-700">{{ ev.description }}</p>

      <section>
        <h2 class="mb-2 text-lg font-semibold text-gray-900">Tickets</h2>
        <div class="flex flex-wrap gap-3">
          <button *ngFor="let t of ticketTypes()" type="button"
                  (click)="activeTicketTypeId.set(t.id)"
                  class="rounded-lg border px-4 py-2 text-left"
                  [class.border-indigo-600]="activeTicketTypeId() === t.id"
                  [class.border-gray-300]="activeTicketTypeId() !== t.id">
            <span class="block font-medium text-gray-900">{{ t.name }}</span>
            <span class="block text-sm text-gray-600">{{ t.price | inr }} · {{ t.availableQuantity }} left</span>
          </button>
        </div>
      </section>

      <section *ngIf="activeTicketTypeId()">
        <h2 class="mb-2 text-lg font-semibold text-gray-900">Select seats</h2>
        <ems-seat-map [eventId]="ev.id" [venueId]="ev.venueId"
                      [selectedSeatIds]="selectedSeatIds()" (seatToggled)="onSeatToggled($event)" />
      </section>

      <div class="flex items-center justify-between border-t border-gray-200 pt-4">
        <span class="text-gray-700">{{ selected().length }} seat(s) selected</span>
        <button [disabled]="selected().length === 0" (click)="checkout()"
                class="rounded-lg bg-indigo-600 px-5 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">
          Proceed to Checkout
        </button>
      </div>
    </article>
  `,
})
export class EventDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private eventService = inject(EventService);
  private ticketTypeService = inject(TicketTypeService);
  private seatService = inject(SeatService);
  private bookingService = inject(BookingService);
  private auth = inject(AuthService);

  protected event = signal<EventDto | null>(null);
  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected activeTicketTypeId = signal<number | null>(null);
  protected selected = signal<SeatReservationDto[]>([]);

  protected selectedSeatIds = () => this.selected().map(r => r.seatId);

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug')!;
    this.loading.set(true);
    this.eventService.getBySlug(slug).subscribe({
      next: ev => {
        this.event.set(ev);
        this.loading.set(false);
        this.ticketTypeService.getActiveByEvent(ev.id).subscribe({
          next: tts => this.ticketTypes.set(tts),
          error: (msg: string) => this.error.set(msg),
        });
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected onSeatToggled(seat: SeatDto): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: this.router.url } });
      return;
    }
    const ttId = this.activeTicketTypeId();
    if (!ttId) { this.error.set('Pick a ticket type first.'); return; }

    const existing = this.selected().find(r => r.seatId === seat.id);
    if (existing) {
      this.seatService.releaseReservation(existing.id).subscribe({
        next: () => this.selected.update(list => list.filter(r => r.seatId !== seat.id)),
        error: (msg: string) => this.error.set(msg),
      });
      return;
    }
    this.seatService.reserve({ eventId: this.event()!.id, seatId: seat.id, ticketTypeId: ttId }).subscribe({
      next: res => this.selected.update(list => [...list, res]),
      error: (msg: string) => this.error.set(msg),
    });
  }

  protected checkout(): void {
    const items = this.selected().map(r => ({ ticketTypeId: r.ticketTypeId, seatId: r.seatId }));
    this.bookingService.create({ eventId: this.event()!.id, items }).subscribe({
      next: booking => this.router.navigate(['/checkout', booking.id]),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-detail.component.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add EMSAngular/src/app/features/events/event-detail
git commit -m "add event detail"
```

---

### Task 17: Checkout page + checkout.routes

**Files:**
- Create: `EMSAngular/src/app/features/events/checkout/checkout.component.ts`
- Create: `EMSAngular/src/app/features/events/checkout.routes.ts`
- Test: `EMSAngular/src/app/features/events/checkout/checkout.component.spec.ts`

**Interfaces:**
- Consumes: `BookingService.getById()`, `PaymentService.initiate/confirm`; `StripePaymentComponent`, `CurrencyInrPipe`, `AlertComponent`, `LoadingSpinnerComponent`.
- Produces: `CHECKOUT_ROUTES` with `':bookingId'` → `CheckoutComponent`. On init loads the booking + initiates payment to get `clientSecret`. On `paymentSucceeded(intentId)` calls `PaymentService.confirm()` then navigates to `/bookings/:id`.

- [ ] **Step 1: Write the failing test** — `checkout.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { CheckoutComponent } from './checkout.component';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';

const booking = { id: 123, userId: 1, eventId: 5, eventTitle: 'Show', bookingReference: 'BK1',
  qrCode: '', totalAmount: 200, bookingStatus: 'Pending', expiresAt: '', createdAt: '', items: [] };

describe('CheckoutComponent', () => {
  let fixture: ComponentFixture<CheckoutComponent>;
  let component: CheckoutComponent;
  let payment: { initiate: jasmine.Spy; confirm: jasmine.Spy };

  beforeEach(() => {
    payment = {
      initiate: jasmine.createSpy().and.returnValue(of({ clientSecret: 'cs_1' })),
      confirm: jasmine.createSpy().and.returnValue(of({ id: 1 })),
    };
    TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '123' } } } },
        { provide: BookingService, useValue: { getById: () => of(booking) } },
        { provide: PaymentService, useValue: payment },
      ],
    });
    fixture = TestBed.createComponent(CheckoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads booking and initiates payment', () => {
    expect(component['booking']()?.id).toBe(123);
    expect(payment.initiate).toHaveBeenCalledWith({ bookingId: 123, currency: 'inr' });
    expect(component['clientSecret']()).toBe('cs_1');
  });

  it('confirms payment and navigates to booking on success', () => {
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate');
    component['onPaymentSucceeded']('pi_123');
    expect(payment.confirm).toHaveBeenCalledWith({ stripePaymentIntentId: 'pi_123' });
    expect(nav).toHaveBeenCalledWith(['/bookings', 123]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/checkout.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `checkout.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';
import { BookingDto } from '../../../core/models/booking.model';
import { StripePaymentComponent } from '../../../shared/components/stripe-payment/stripe-payment.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-checkout',
  standalone: true,
  imports: [CommonModule, StripePaymentComponent, LoadingSpinnerComponent, AlertComponent, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <div *ngIf="booking() as b" class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <section class="rounded-lg border border-gray-200 bg-white p-6">
        <h2 class="mb-3 text-lg font-semibold text-gray-900">Order summary</h2>
        <p class="font-medium text-gray-900">{{ b.eventTitle }}</p>
        <ul class="mt-3 space-y-1 text-sm text-gray-600">
          <li *ngFor="let item of b.items" class="flex justify-between">
            <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
            <span>{{ item.unitPrice | inr }}</span>
          </li>
        </ul>
        <div class="mt-3 flex justify-between border-t border-gray-200 pt-3 font-semibold text-gray-900">
          <span>Total</span><span>{{ b.totalAmount | inr }}</span>
        </div>
      </section>

      <section class="rounded-lg border border-gray-200 bg-white p-6">
        <h2 class="mb-3 text-lg font-semibold text-gray-900">Payment</h2>
        <ems-stripe-payment *ngIf="clientSecret()" [clientSecret]="clientSecret()!"
                            (paymentSucceeded)="onPaymentSucceeded($event)"
                            (paymentFailed)="error.set($event)" />
      </section>
    </div>
  `,
})
export class CheckoutComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private bookingService = inject(BookingService);
  private paymentService = inject(PaymentService);

  protected booking = signal<BookingDto | null>(null);
  protected clientSecret = signal<string | null>(null);
  protected loading = signal(false);
  protected error = signal('');

  ngOnInit(): void {
    const bookingId = Number(this.route.snapshot.paramMap.get('bookingId'));
    this.loading.set(true);
    this.bookingService.getById(bookingId).subscribe({
      next: b => {
        this.booking.set(b);
        this.loading.set(false);
        this.paymentService.initiate({ bookingId, currency: 'inr' }).subscribe({
          next: p => this.clientSecret.set(p.clientSecret),
          error: (msg: string) => this.error.set(msg),
        });
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected onPaymentSucceeded(intentId: string): void {
    this.paymentService.confirm({ stripePaymentIntentId: intentId }).subscribe({
      next: () => this.router.navigate(['/bookings', this.booking()!.id]),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
```

- [ ] **Step 4: Implement `checkout.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { CheckoutComponent } from './checkout/checkout.component';

export const CHECKOUT_ROUTES: Routes = [
  { path: ':bookingId', component: CheckoutComponent },
];
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/checkout.component.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add EMSAngular/src/app/features/events/checkout EMSAngular/src/app/features/events/checkout.routes.ts
git commit -m "add checkout page"
```

---

## Phase 7 — Bookings Feature

### Task 18: Bookings list + detail + bookings.routes

**Files:**
- Create: `EMSAngular/src/app/features/bookings/booking-list/booking-list.component.ts`
- Create: `EMSAngular/src/app/features/bookings/booking-detail/booking-detail.component.ts`
- Create: `EMSAngular/src/app/features/bookings/bookings.routes.ts`
- Test: `EMSAngular/src/app/features/bookings/booking-list/booking-list.component.spec.ts`

**Interfaces:**
- Consumes: `BookingService.getMyBookings/getById/cancel`; `BookingQrComponent`, `PaginationComponent`, `LoadingSpinnerComponent`, `AlertComponent`, `IstDatePipe`, `CurrencyInrPipe`, `RouterLink`.
- Produces: `BOOKINGS_ROUTES` (`''` → list, `':id'` → detail). List has `bookings`, `loading`, `error`, `page`, `totalPages` signals. Detail shows the QR, items, and a Cancel button when `bookingStatus` is `Pending` or `Confirmed`.

- [ ] **Step 1: Write the failing test** — `booking-list.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BookingListComponent } from './booking-list.component';
import { BookingService } from '../../../core/services/booking.service';

const paged = {
  items: [{ id: 1, userId: 1, eventId: 5, eventTitle: 'Show', bookingReference: 'BK1', qrCode: '',
    totalAmount: 200, bookingStatus: 'Confirmed', expiresAt: '', createdAt: '2026-06-01T10:00:00', items: [] }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('BookingListComponent', () => {
  let fixture: ComponentFixture<BookingListComponent>;
  let component: BookingListComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BookingListComponent],
      providers: [provideRouter([]), { provide: BookingService, useValue: { getMyBookings: () => of(paged) } }],
    });
    fixture = TestBed.createComponent(BookingListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads my bookings on init', () => {
    expect(component['bookings']().length).toBe(1);
    expect(component['loading']()).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/booking-list.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `booking-list.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto } from '../../../core/models/booking.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-booking-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">My Bookings</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="space-y-3">
      <a *ngFor="let b of bookings()" [routerLink]="['/bookings', b.id]"
         class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-4 hover:shadow-sm">
        <div>
          <p class="font-medium text-gray-900">{{ b.eventTitle }}</p>
          <p class="text-sm text-gray-500">{{ b.bookingReference }} · {{ b.createdAt | istDate }}</p>
        </div>
        <div class="text-right">
          <span class="rounded-full px-2 py-0.5 text-xs font-medium"
                [class.bg-green-50]="b.bookingStatus === 'Confirmed'" [class.text-green-700]="b.bookingStatus === 'Confirmed'"
                [class.bg-amber-50]="b.bookingStatus === 'Pending'" [class.text-amber-700]="b.bookingStatus === 'Pending'"
                [class.bg-red-50]="b.bookingStatus === 'Cancelled'" [class.text-red-700]="b.bookingStatus === 'Cancelled'"
                [class.bg-gray-100]="b.bookingStatus === 'Attended'" [class.text-gray-700]="b.bookingStatus === 'Attended'">
            {{ b.bookingStatus }}
          </span>
          <p class="mt-1 text-sm text-gray-900">{{ b.totalAmount | inr }}</p>
        </div>
      </a>
      <p *ngIf="bookings().length === 0" class="py-10 text-center text-gray-500">No bookings yet.</p>
    </div>

    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class BookingListComponent implements OnInit {
  private bookingService = inject(BookingService);

  protected bookings = signal<BookingDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);

  ngOnInit(): void { this.load(); }

  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  private load(): void {
    this.loading.set(true);
    this.bookingService.getMyBookings({ page: this.page(), pageSize: 10 }).subscribe({
      next: res => { this.bookings.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 4: Implement `booking-detail.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto } from '../../../core/models/booking.model';
import { BookingQrComponent } from '../../../shared/components/booking-qr/booking-qr.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-booking-detail',
  standalone: true,
  imports: [CommonModule, BookingQrComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <article *ngIf="booking() as b" class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <section class="rounded-lg border border-gray-200 bg-white p-6">
        <h1 class="text-xl font-semibold text-gray-900">{{ b.eventTitle }}</h1>
        <p class="text-sm text-gray-500">{{ b.bookingReference }} · {{ b.createdAt | istDate }}</p>
        <ul class="mt-4 space-y-1 text-sm text-gray-700">
          <li *ngFor="let item of b.items" class="flex justify-between">
            <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
            <span>{{ item.unitPrice | inr }}</span>
          </li>
        </ul>
        <div class="mt-3 flex justify-between border-t border-gray-200 pt-3 font-semibold text-gray-900">
          <span>Total</span><span>{{ b.totalAmount | inr }}</span>
        </div>
        <button *ngIf="canCancel(b)" (click)="cancel(b.id)"
                class="mt-4 rounded-lg bg-red-600 px-4 py-2 text-white hover:bg-red-700">Cancel booking</button>
      </section>

      <section class="flex items-center justify-center rounded-lg border border-gray-200 bg-white p-6">
        <ems-booking-qr [qrCode]="b.qrCode" />
      </section>
    </article>
  `,
})
export class BookingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private bookingService = inject(BookingService);

  protected booking = signal<BookingDto | null>(null);
  protected loading = signal(false);
  protected error = signal('');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);
    this.bookingService.getById(id).subscribe({
      next: b => { this.booking.set(b); this.loading.set(false); },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected canCancel(b: BookingDto): boolean {
    return b.bookingStatus === 'Pending' || b.bookingStatus === 'Confirmed';
  }

  protected cancel(id: number): void {
    this.bookingService.cancel(id).subscribe({
      next: b => this.booking.set(b),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
```

- [ ] **Step 5: Implement `bookings.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { BookingListComponent } from './booking-list/booking-list.component';
import { BookingDetailComponent } from './booking-detail/booking-detail.component';

export const BOOKINGS_ROUTES: Routes = [
  { path: '', component: BookingListComponent },
  { path: ':id', component: BookingDetailComponent },
];
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/booking-list.component.spec.ts'`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add EMSAngular/src/app/features/bookings
git commit -m "add bookings feature"
```

---

## Phase 8 — Organizer Feature

### Task 19: Organizer events list, event form, ticket types, scanner + organizer.routes

**Files:**
- Create: `EMSAngular/src/app/features/organizer/event-list/organizer-event-list.component.ts`
- Create: `EMSAngular/src/app/features/organizer/event-form/event-form.component.ts`
- Create: `EMSAngular/src/app/features/organizer/ticket-types/ticket-types.component.ts`
- Create: `EMSAngular/src/app/features/organizer/scanner/scanner.component.ts`
- Create: `EMSAngular/src/app/features/organizer/organizer.routes.ts`
- Test: `EMSAngular/src/app/features/organizer/event-form/event-form.component.spec.ts`

**Interfaces:**
- Consumes: `EventService` (`getMyEvents/create/update/getById/submit/cancel/delete`), `VenueService.list()`, `TicketTypeService` (`getByEvent/create/delete`), `BookingService.validateQr()`, `AuthService.currentUser()`.
- Produces: `ORGANIZER_ROUTES`:
  - `events` → `OrganizerEventListComponent`
  - `events/new` → `EventFormComponent` (create mode)
  - `events/:id/edit` → `EventFormComponent` (edit mode, detects `:id`)
  - `events/:id/tickets` → `TicketTypesComponent`
  - `events/:id/bookings` → `ScannerComponent`
  - `''` → redirect to `events`
- `EventFormComponent` uses a reactive form mirroring `CreateEventRequest`/`UpdateEventRequest`. `datetime-local` inputs bind to ISO strings.

- [ ] **Step 1: Write the failing test** — `event-form.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { EventFormComponent } from './event-form.component';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';

describe('EventFormComponent (create mode)', () => {
  let fixture: ComponentFixture<EventFormComponent>;
  let component: EventFormComponent;
  let eventService: { create: jasmine.Spy };

  beforeEach(() => {
    eventService = { create: jasmine.createSpy().and.returnValue(of({ id: 9 })) };
    TestBed.configureTestingModule({
      imports: [EventFormComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: EventService, useValue: eventService },
        { provide: VenueService, useValue: { list: () => of([{ id: 1, name: 'Hall', address: '', city: 'X', totalCapacity: 10, layoutConfig: '', createdAt: '' }]) } },
      ],
    });
    fixture = TestBed.createComponent(EventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads venues and is in create mode', () => {
    expect(component['isEdit']()).toBe(false);
    expect(component['venues']().length).toBe(1);
  });

  it('does not submit when the form is invalid', () => {
    component.submit();
    expect(eventService.create).not.toHaveBeenCalled();
  });

  it('creates an event and navigates on valid submit', () => {
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate');
    component.form.setValue({
      venueId: 1, title: 'My Event', description: 'A great event',
      startTime: '2026-07-01T19:00', endTime: '2026-07-01T22:00',
      imageUrl: 'http://img/x.jpg', category: 'Music',
    });
    component.submit();
    expect(eventService.create).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/organizer/events']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-form.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `event-form.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { VenueDto } from '../../../core/models/venue.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">{{ isEdit() ? 'Edit' : 'Create' }} Event</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <form [formGroup]="form" (ngSubmit)="submit()" class="max-w-xl space-y-4">
      <select formControlName="venueId" class="w-full rounded-lg border border-gray-300 px-3 py-2" [class.hidden]="isEdit()">
        <option [ngValue]="0" disabled>Select venue…</option>
        <option *ngFor="let v of venues()" [ngValue]="v.id">{{ v.name }} — {{ v.city }}</option>
      </select>
      <input formControlName="title" placeholder="Title" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
      <textarea formControlName="description" placeholder="Description" rows="4" class="w-full rounded-lg border border-gray-300 px-3 py-2"></textarea>
      <label class="block text-sm text-gray-600">Start time
        <input formControlName="startTime" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" />
      </label>
      <label class="block text-sm text-gray-600">End time
        <input formControlName="endTime" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" />
      </label>
      <input formControlName="imageUrl" placeholder="Image URL" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="category" placeholder="Category" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-5 py-2 text-white hover:bg-indigo-700">{{ isEdit() ? 'Save' : 'Create' }}</button>
    </form>
  `,
})
export class EventFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);
  private venueService = inject(VenueService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected venues = signal<VenueDto[]>([]);
  protected error = signal('');
  private eventId = signal<number | null>(null);
  protected isEdit = computed(() => this.eventId() !== null);

  protected form = this.fb.nonNullable.group({
    venueId: [0, [Validators.min(1)]],
    title: ['', [Validators.required, Validators.minLength(2)]],
    description: ['', [Validators.required, Validators.minLength(1)]],
    startTime: ['', Validators.required],
    endTime: ['', Validators.required],
    imageUrl: ['', [Validators.required]],
    category: ['', [Validators.required, Validators.minLength(2)]],
  });

  ngOnInit(): void {
    this.venueService.list().subscribe({ next: v => this.venues.set(v), error: (m: string) => this.error.set(m) });
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.eventId.set(Number(idParam));
      this.eventService.getById(Number(idParam)).subscribe({
        next: ev => this.form.patchValue({
          venueId: ev.venueId, title: ev.title, description: ev.description,
          startTime: ev.startTime.slice(0, 16), endTime: ev.endTime.slice(0, 16),
          imageUrl: ev.imageUrl, category: ev.category,
        }),
        error: (m: string) => this.error.set(m),
      });
    }
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const id = this.eventId();
    const done = { next: () => this.router.navigate(['/organizer/events']), error: (m: string) => this.error.set(m) };
    if (id !== null) {
      this.eventService.update(id, {
        title: v.title, description: v.description, startTime: v.startTime,
        endTime: v.endTime, imageUrl: v.imageUrl, category: v.category,
      }).subscribe(done);
    } else {
      this.eventService.create(v).subscribe(done);
    }
  }
}
```

- [ ] **Step 4: Implement `organizer-event-list.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { EventDto } from '../../../core/models/event.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-organizer-event-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-4 flex items-center justify-between">
      <h1 class="text-2xl font-semibold text-gray-900">My Events</h1>
      <a routerLink="/organizer/events/new" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">New Event</a>
    </div>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <table *ngIf="!loading()" class="w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-sm">
      <thead class="bg-gray-50 text-left text-gray-600">
        <tr><th class="p-3">Title</th><th class="p-3">Start</th><th class="p-3">Status</th><th class="p-3">Actions</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let ev of events()" class="border-t border-gray-100">
          <td class="p-3 font-medium text-gray-900">{{ ev.title }}</td>
          <td class="p-3 text-gray-600">{{ ev.startTime | istDate }}</td>
          <td class="p-3 text-gray-600">{{ ev.status }}</td>
          <td class="p-3">
            <div class="flex flex-wrap gap-2">
              <a [routerLink]="['/organizer/events', ev.id, 'edit']" class="text-indigo-600 hover:underline">Edit</a>
              <a [routerLink]="['/organizer/events', ev.id, 'tickets']" class="text-indigo-600 hover:underline">Tickets</a>
              <a [routerLink]="['/organizer/events', ev.id, 'bookings']" class="text-indigo-600 hover:underline">Scan</a>
              <button *ngIf="ev.status === 'Draft' || ev.status === 'Rejected'" (click)="submitEvent(ev.id)" class="text-green-600 hover:underline">Submit</button>
              <button *ngIf="ev.status !== 'Cancelled'" (click)="cancelEvent(ev.id)" class="text-red-600 hover:underline">Cancel</button>
            </div>
          </td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!loading() && events().length === 0" class="py-10 text-center text-gray-500">No events yet.</p>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class OrganizerEventListComponent implements OnInit {
  private eventService = inject(EventService);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);

  ngOnInit(): void { this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected submitEvent(id: number): void {
    this.eventService.submit(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }
  protected cancelEvent(id: number): void {
    this.eventService.cancel(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.eventService.getMyEvents(this.page(), 10).subscribe({
      next: res => { this.events.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 5: Implement `ticket-types.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-ticket-types',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Ticket Types</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <ul class="mb-6 space-y-2">
      <li *ngFor="let t of ticketTypes()" class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-3">
        <span>{{ t.name }} ({{ t.seatType }}) · {{ t.price | inr }} · {{ t.availableQuantity }}/{{ t.totalQuantity }}</span>
        <button (click)="remove(t.id)" class="text-red-600 hover:underline">Delete</button>
      </li>
      <li *ngIf="ticketTypes().length === 0" class="text-gray-500">No ticket types yet.</li>
    </ul>

    <form [formGroup]="form" (ngSubmit)="add()" class="grid max-w-xl grid-cols-2 gap-3">
      <input formControlName="name" placeholder="Name" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="seatType" placeholder="Seat type" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="price" type="number" placeholder="Price" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="totalQuantity" type="number" placeholder="Quantity" class="rounded-lg border border-gray-300 px-3 py-2" />
      <label class="text-sm text-gray-600">Sale start<input formControlName="saleStart" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" /></label>
      <label class="text-sm text-gray-600">Sale end<input formControlName="saleEnd" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" /></label>
      <button type="submit" class="col-span-2 rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Add ticket type</button>
    </form>
  `,
})
export class TicketTypesComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private service = inject(TicketTypeService);

  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected error = signal('');
  private eventId = Number(this.route.snapshot.paramMap.get('id'));

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    seatType: ['', [Validators.required, Validators.minLength(2)]],
    price: [0, [Validators.min(0)]],
    totalQuantity: [1, [Validators.min(1)]],
    saleStart: ['', Validators.required],
    saleEnd: ['', Validators.required],
  });

  ngOnInit(): void { this.load(); }

  protected add(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.service.create({ eventId: this.eventId, ...this.form.getRawValue() }).subscribe({
      next: () => { this.form.reset(); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.service.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.service.getByEvent(this.eventId).subscribe({
      next: t => this.ticketTypes.set(t), error: (m: string) => this.error.set(m),
    });
  }
}
```

- [ ] **Step 6: Implement `scanner.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { BookingDto } from '../../../core/models/booking.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-scanner',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Validate Ticket</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-alert type="success" [message]="success()" (dismissed)="success.set('')" />

    <form [formGroup]="form" (ngSubmit)="validate()" class="flex max-w-xl gap-3">
      <input formControlName="qrPayload" placeholder="Paste QR payload" class="flex-1 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Validate</button>
    </form>

    <div *ngIf="result() as b" class="mt-4 rounded-lg border border-gray-200 bg-white p-4">
      <p class="font-medium text-gray-900">{{ b.eventTitle }} — {{ b.bookingReference }}</p>
      <p class="text-sm text-gray-600">Status: {{ b.bookingStatus }}</p>
    </div>
  `,
})
export class ScannerComponent {
  private fb = inject(FormBuilder);
  private bookingService = inject(BookingService);
  private auth = inject(AuthService);

  protected error = signal('');
  protected success = signal('');
  protected result = signal<BookingDto | null>(null);
  protected form = this.fb.nonNullable.group({ qrPayload: ['', Validators.required] });

  protected validate(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.error.set(''); this.success.set('');
    const scannedBy = this.auth.currentUser()?.id ?? 0;
    this.bookingService.validateQr({ qrPayload: this.form.getRawValue().qrPayload, scannedBy }).subscribe({
      next: b => { this.result.set(b); this.success.set('Ticket valid — marked attended.'); },
      error: (m: string) => { this.error.set(m); this.result.set(null); },
    });
  }
}
```

- [ ] **Step 7: Implement `organizer.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { OrganizerEventListComponent } from './event-list/organizer-event-list.component';
import { EventFormComponent } from './event-form/event-form.component';
import { TicketTypesComponent } from './ticket-types/ticket-types.component';
import { ScannerComponent } from './scanner/scanner.component';

export const ORGANIZER_ROUTES: Routes = [
  { path: 'events', component: OrganizerEventListComponent },
  { path: 'events/new', component: EventFormComponent },
  { path: 'events/:id/edit', component: EventFormComponent },
  { path: 'events/:id/tickets', component: TicketTypesComponent },
  { path: 'events/:id/bookings', component: ScannerComponent },
  { path: '', pathMatch: 'full', redirectTo: 'events' },
];
```

- [ ] **Step 8: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-form.component.spec.ts'`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add EMSAngular/src/app/features/organizer
git commit -m "add organizer feature"
```

---

## Phase 9 — Admin Feature

### Task 20: Event approvals, organizer requests, users, venues, seats + admin.routes

**Files:**
- Create: `EMSAngular/src/app/features/admin/event-approvals/event-approvals.component.ts`
- Create: `EMSAngular/src/app/features/admin/organizer-requests/organizer-requests.component.ts`
- Create: `EMSAngular/src/app/features/admin/users/admin-users.component.ts`
- Create: `EMSAngular/src/app/features/admin/venues/admin-venues.component.ts`
- Create: `EMSAngular/src/app/features/admin/seats/admin-seats.component.ts`
- Create: `EMSAngular/src/app/features/admin/admin.routes.ts`
- Test: `EMSAngular/src/app/features/admin/event-approvals/event-approvals.component.spec.ts`

**Interfaces:**
- Consumes: `AdminService` (all methods), `VenueService` (CRUD), `SeatService` (`getByVenue/bulkCreate/delete`); shared components/pipes.
- Produces: `ADMIN_ROUTES`:
  - `events` → `EventApprovalsComponent`
  - `organizer-requests` → `OrganizerRequestsComponent`
  - `users` → `AdminUsersComponent`
  - `venues` → `AdminVenuesComponent`
  - `venues/:id/seats` → `AdminSeatsComponent`
  - `''` → redirect to `events`

- [ ] **Step 1: Write the failing test** — `event-approvals.component.spec.ts`

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { EventApprovalsComponent } from './event-approvals.component';
import { AdminService } from '../../../core/services/admin.service';

const paged = {
  items: [{ id: 3, organizerId: 1, venueId: 1, title: 'Pending Show', description: '', status: 'PendingApproval',
    startTime: '2026-07-01T19:00:00', endTime: '2026-07-01T22:00:00', imageUrl: '', category: 'Music',
    slug: 'pending-show', createdAt: '' }],
  totalCount: 1, page: 1, pageSize: 10, totalPages: 1,
};

describe('EventApprovalsComponent', () => {
  let fixture: ComponentFixture<EventApprovalsComponent>;
  let component: EventApprovalsComponent;
  let admin: { getPendingEvents: jasmine.Spy; approveEvent: jasmine.Spy; rejectEvent: jasmine.Spy };

  beforeEach(() => {
    admin = {
      getPendingEvents: jasmine.createSpy().and.returnValue(of(paged)),
      approveEvent: jasmine.createSpy().and.returnValue(of({})),
      rejectEvent: jasmine.createSpy().and.returnValue(of({})),
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
    admin.getPendingEvents.calls.reset();
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-approvals.component.spec.ts'`
Expected: FAIL — component not found.

- [ ] **Step 3: Implement `event-approvals.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { EventDto } from '../../../core/models/event.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-approvals',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Pending Events</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="space-y-3">
      <div *ngFor="let ev of events()" class="rounded-lg border border-gray-200 bg-white p-4">
        <p class="font-medium text-gray-900">{{ ev.title }}</p>
        <p class="text-sm text-gray-500">{{ ev.startTime | istDate }} · {{ ev.category }}</p>
        <p class="mt-1 text-sm text-gray-700">{{ ev.description }}</p>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <button (click)="approve(ev.id)" class="rounded-lg bg-green-600 px-3 py-1 text-sm text-white hover:bg-green-700">Approve</button>
          <input [ngModel]="reasons()[ev.id] ?? ''" (ngModelChange)="setReason(ev.id, $event)"
                 placeholder="Rejection reason" class="flex-1 rounded-lg border border-gray-300 px-3 py-1 text-sm" />
          <button (click)="reject(ev.id)" class="rounded-lg bg-red-600 px-3 py-1 text-sm text-white hover:bg-red-700">Reject</button>
        </div>
      </div>
      <p *ngIf="events().length === 0" class="py-10 text-center text-gray-500">No pending events.</p>
    </div>
  `,
})
export class EventApprovalsComponent implements OnInit {
  private admin = inject(AdminService);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected reasons = signal<Record<number, string>>({});

  ngOnInit(): void { this.load(); }

  protected setReason(id: number, value: string): void {
    this.reasons.update(r => ({ ...r, [id]: value }));
  }

  protected approve(id: number): void {
    this.admin.approveEvent(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  protected reject(id: number): void {
    const reason = this.reasons()[id] || undefined;
    this.admin.rejectEvent(id, { reason }).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getPendingEvents(1, 50).subscribe({
      next: res => { this.events.set(res.items); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 4: Implement `organizer-requests.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../../core/services/admin.service';
import { OrganizerRequestDto } from '../../../core/models/admin.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-organizer-requests',
  standalone: true,
  imports: [CommonModule, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Organizer Requests</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <table *ngIf="!loading()" class="w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-sm">
      <thead class="bg-gray-50 text-left text-gray-600">
        <tr><th class="p-3">User</th><th class="p-3">Requested</th><th class="p-3">Actions</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let r of requests()" class="border-t border-gray-100">
          <td class="p-3"><span class="font-medium text-gray-900">{{ r.userName }}</span><br /><span class="text-gray-500">{{ r.userEmail }}</span></td>
          <td class="p-3 text-gray-600">{{ r.requestedAt | istDate }}</td>
          <td class="p-3">
            <button (click)="approve(r.id)" class="mr-2 text-green-600 hover:underline">Approve</button>
            <button (click)="reject(r.id)" class="text-red-600 hover:underline">Reject</button>
          </td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!loading() && requests().length === 0" class="py-10 text-center text-gray-500">No pending requests.</p>
  `,
})
export class OrganizerRequestsComponent implements OnInit {
  private admin = inject(AdminService);

  protected requests = signal<OrganizerRequestDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');

  ngOnInit(): void { this.load(); }

  protected approve(id: number): void {
    this.admin.approveOrganizerRequest(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }
  protected reject(id: number): void {
    this.admin.rejectOrganizerRequest(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getOrganizerRequests({ status: 'Pending', page: 1, pageSize: 50 }).subscribe({
      next: res => { this.requests.set(res.items); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 5: Implement `admin-users.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { User } from '../../../core/models/user.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PaginationComponent, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Users</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-4 flex gap-3">
      <input formControlName="query" placeholder="Search name or email…" class="flex-1 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Search</button>
    </form>
    <ems-loading-spinner *ngIf="loading()" />

    <table *ngIf="!loading()" class="w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-sm">
      <thead class="bg-gray-50 text-left text-gray-600">
        <tr><th class="p-3">Name</th><th class="p-3">Email</th><th class="p-3">Role</th><th class="p-3">Active</th><th class="p-3"></th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let u of users()" class="border-t border-gray-100">
          <td class="p-3 font-medium text-gray-900">{{ u.name }}</td>
          <td class="p-3 text-gray-600">{{ u.email }}</td>
          <td class="p-3 text-gray-600">{{ u.role }}</td>
          <td class="p-3 text-gray-600">{{ u.isActive ? 'Yes' : 'No' }}</td>
          <td class="p-3"><button (click)="remove(u.id)" class="text-red-600 hover:underline">Delete</button></td>
        </tr>
      </tbody>
    </table>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class AdminUsersComponent implements OnInit {
  private admin = inject(AdminService);
  private fb = inject(FormBuilder);

  protected users = signal<User[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);
  protected filters = this.fb.nonNullable.group({ query: '' });

  ngOnInit(): void { this.load(); }
  protected applyFilters(): void { this.page.set(1); this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected remove(id: number): void {
    this.admin.deleteUser(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getUsers({ query: this.filters.getRawValue().query, page: this.page(), pageSize: 20 }).subscribe({
      next: res => { this.users.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 6: Implement `admin-venues.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { VenueService } from '../../../core/services/venue.service';
import { VenueDto } from '../../../core/models/venue.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-venues',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Venues</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <ul *ngIf="!loading()" class="mb-6 space-y-2">
      <li *ngFor="let v of venues()" class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-3">
        <span><span class="font-medium text-gray-900">{{ v.name }}</span> — {{ v.city }} (cap {{ v.totalCapacity }})</span>
        <span class="flex gap-3">
          <a [routerLink]="['/admin/venues', v.id, 'seats']" class="text-indigo-600 hover:underline">Seats</a>
          <button (click)="remove(v.id)" class="text-red-600 hover:underline">Delete</button>
        </span>
      </li>
      <li *ngIf="venues().length === 0" class="text-gray-500">No venues yet.</li>
    </ul>

    <form [formGroup]="form" (ngSubmit)="add()" class="grid max-w-xl grid-cols-2 gap-3">
      <input formControlName="name" placeholder="Name" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="city" placeholder="City" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="address" placeholder="Address" class="col-span-2 rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="totalCapacity" type="number" placeholder="Capacity" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="layoutConfig" placeholder="Layout config" class="rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="col-span-2 rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Add venue</button>
    </form>
  `,
})
export class AdminVenuesComponent implements OnInit {
  private venueService = inject(VenueService);
  private fb = inject(FormBuilder);

  protected venues = signal<VenueDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    address: ['', [Validators.required, Validators.minLength(5)]],
    city: ['', [Validators.required, Validators.minLength(2)]],
    totalCapacity: [1, [Validators.min(1)]],
    layoutConfig: ['{}', Validators.required],
  });

  ngOnInit(): void { this.load(); }

  protected add(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.venueService.create(this.form.getRawValue()).subscribe({
      next: () => { this.form.reset({ totalCapacity: 1, layoutConfig: '{}' }); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.venueService.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.venueService.list().subscribe({
      next: v => { this.venues.set(v); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 7: Implement `admin-seats.component.ts`**

```ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SeatService } from '../../../core/services/seat.service';
import { SeatDto } from '../../../core/models/seat.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-seats',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Seats</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <form [formGroup]="form" (ngSubmit)="bulkCreate()" class="mb-6 grid max-w-xl grid-cols-2 gap-3">
      <input formControlName="section" placeholder="Section" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="row" placeholder="Row" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="startNumber" type="number" placeholder="Start #" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="endNumber" type="number" placeholder="End #" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="seatType" placeholder="Seat type" class="col-span-2 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="col-span-2 rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Bulk create row</button>
    </form>

    <div *ngIf="!loading()" class="flex flex-wrap gap-2">
      <span *ngFor="let s of seats()" class="flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-2 py-1 text-sm">
        {{ s.section }}-{{ s.row }}-{{ s.seatNumber }}
        <button (click)="remove(s.id)" class="text-red-600">×</button>
      </span>
      <p *ngIf="seats().length === 0" class="text-gray-500">No seats yet.</p>
    </div>
  `,
})
export class AdminSeatsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private seatService = inject(SeatService);
  private fb = inject(FormBuilder);

  protected seats = signal<SeatDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  private venueId = Number(this.route.snapshot.paramMap.get('id'));

  protected form = this.fb.nonNullable.group({
    section: ['', [Validators.required, Validators.minLength(1)]],
    row: ['', [Validators.required, Validators.minLength(1)]],
    startNumber: [1, [Validators.min(1)]],
    endNumber: [1, [Validators.min(1)]],
    seatType: ['', [Validators.required, Validators.minLength(2)]],
  });

  ngOnInit(): void { this.load(); }

  protected bulkCreate(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.seatService.bulkCreate({ venueId: this.venueId, ...this.form.getRawValue() }).subscribe({
      next: () => { this.form.reset({ startNumber: 1, endNumber: 1 }); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.seatService.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.seatService.getByVenue(this.venueId).subscribe({
      next: s => { this.seats.set(s); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
```

- [ ] **Step 8: Implement `admin.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { EventApprovalsComponent } from './event-approvals/event-approvals.component';
import { OrganizerRequestsComponent } from './organizer-requests/organizer-requests.component';
import { AdminUsersComponent } from './users/admin-users.component';
import { AdminVenuesComponent } from './venues/admin-venues.component';
import { AdminSeatsComponent } from './seats/admin-seats.component';

export const ADMIN_ROUTES: Routes = [
  { path: 'events', component: EventApprovalsComponent },
  { path: 'organizer-requests', component: OrganizerRequestsComponent },
  { path: 'users', component: AdminUsersComponent },
  { path: 'venues', component: AdminVenuesComponent },
  { path: 'venues/:id/seats', component: AdminSeatsComponent },
  { path: '', pathMatch: 'full', redirectTo: 'events' },
];
```

- [ ] **Step 9: Run test to verify it passes**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless --include='**/event-approvals.component.spec.ts'`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add EMSAngular/src/app/features/admin
git commit -m "add admin feature"
```

---

## Phase 10 — Full Integration

### Task 21: Full build + complete test suite

**Files:**
- No new files. This task wires nothing new; it verifies the whole app compiles and every spec passes now that all lazy route files exist.

- [ ] **Step 1: Run the full production build**

Run: `cd EMSAngular && ng build`
Expected: build succeeds with no unresolved `loadChildren` imports.

- [ ] **Step 2: Run the entire test suite**

Run: `cd EMSAngular && ng test --watch=false --browsers=ChromeHeadless`
Expected: all specs PASS.

- [ ] **Step 3: Smoke-test against the running API (manual)**

Run the backend (`dotnet run --project EventManagementSystem/EMSApplicationLayer/EMSApplicationLayer.csproj`) and `ng serve`. Visit `http://localhost:4200`, confirm: events list loads, login with a seeded user (`Test@1234`), browse → seat select → checkout renders the Stripe element.

- [ ] **Step 4: Commit (if any lockfile/config changed)**

```bash
git add -A
git commit -m "verify full build"
```

---

## Self-Review Notes

- **Spec coverage:** all six spec sections map to tasks — structure/models (T0–T1), services/state (T2–T7), routing/guards (T3–T4, T13), UI flows (T15–T20), shared components/design system (T8–T13), error handling/testing (every task's `extractError` + specs).
- **Standalone vs NgModules:** the spec named NgModules with lazy loading; this plan implements the equivalent via standalone components + `loadChildren` route files, which is Angular's current idiom and what `ng new` scaffolds. The lazy boundary per feature is preserved. Flagged inline in Task 13.
- **Port correction:** API base URL is `5222` (verified from `launchSettings.json`), not the `5062` the spec/CLAUDE.md originally stated.
- **Type consistency:** `SeatReservationDto` used consistently in seat reserve flow; `ReviewRequest` shared by event + organizer-request review; `PagedResult<T>` shape matches the backend.
