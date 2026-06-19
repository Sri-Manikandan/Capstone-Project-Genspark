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
