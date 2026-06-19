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
