import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap } from 'rxjs';
import { AuthService } from '../services/auth.service';

const AUTH_PATHS = ['/Auth/login', '/Auth/register', '/Auth/refresh',
  '/Auth/forgot-password', '/Auth/reset-password'];

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const isAuthPath = AUTH_PATHS.some(p => req.url.includes(p));
  if (isAuthPath) return next(req);

  const token = localStorage.getItem('ems_access_token');
  if (!token) return next(req);

  const withToken = (t: string) =>
    next(req.clone({ setHeaders: { Authorization: `Bearer ${t}` } }));

  // Proactively refresh an already-expired access token so the request carries a
  // valid one, instead of firing a request that 401s and relying on the reactive retry.
  if (auth.isAccessTokenExpired() && localStorage.getItem('ems_refresh_token')) {
    return auth.refreshShared().pipe(
      switchMap(res => withToken(res.accessToken)),
      catchError(() => withToken(token)), // refresh failed — let authErrorInterceptor handle the 401
    );
  }

  return withToken(token);
};
