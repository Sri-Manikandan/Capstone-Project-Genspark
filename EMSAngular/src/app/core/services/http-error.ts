import { HttpErrorResponse, HttpParams } from '@angular/common/http';

export function extractError(err: HttpErrorResponse): string {
  const body = err.error;
  if (typeof body === 'string' && body.trim()) return body;
  if (body && typeof body === 'object') {
    // Preferred envelope from ExceptionMiddleware / InvalidModelStateResponseFactory.
    if (typeof body.error === 'string' && body.error) return body.error;
    if (typeof body.message === 'string' && body.message) return body.message;
    // Fallback for ASP.NET ValidationProblemDetails: { errors: { field: [msg] }, title }.
    if (body.errors && typeof body.errors === 'object') {
      const first = Object.values(body.errors as Record<string, string[]>)
        .flat()
        .find((m) => typeof m === 'string' && m.trim());
      if (first) return first;
    }
    if (typeof body.title === 'string' && body.title) return body.title;
  }
  return err.message ?? 'Unexpected error';
}

export function toHttpParams(obj: Record<string, unknown>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(obj)) {
    if (value === undefined || value === null || value === '') continue;
    params = params.set(key, String(value));
  }
  return params;
}
