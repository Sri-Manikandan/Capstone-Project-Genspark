import { HttpHeaders } from '@angular/common/http';

/** Builds the request options that carry the Idempotency-Key header the API expects. */
export function idempotencyHeader(key: string): { headers: HttpHeaders } {
  return { headers: new HttpHeaders({ 'Idempotency-Key': key }) };
}

/** Generates a fresh idempotency key. Reuse the same value across retries of one operation. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}
