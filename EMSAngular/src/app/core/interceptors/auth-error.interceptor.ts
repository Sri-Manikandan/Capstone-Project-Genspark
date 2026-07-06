import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  // A 401 from any auth endpoint (login, refresh, reset…) is a genuine credential
  // error that must surface to the user — never turn it into a token-refresh attempt.
  const isAuthCall = req.url.includes('/Auth/');

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !isAuthCall && localStorage.getItem('ems_refresh_token')) {
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
