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
