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
    return this.http.post<BookingDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getById(id: number): Observable<BookingDto> {
    return this.http.get<BookingDto>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getByReference(reference: string): Observable<BookingDto> {
    return this.http.get<BookingDto>(`${this.base}/reference/${reference}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getMyBookings(req: BookingQueryRequest): Observable<PagedResult<BookingDto>> {
    return this.http.get<PagedResult<BookingDto>>(`${this.base}/my`, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getByEvent(eventId: number, req: BookingQueryRequest): Observable<PagedResult<BookingDto>> {
    return this.http.get<PagedResult<BookingDto>>(`${this.base}/event/${eventId}`, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  cancel(id: number): Observable<BookingDto> {
    return this.http.post<BookingDto>(`${this.base}/${id}/cancel`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  validateQr(req: ValidateQrRequest): Observable<BookingDto> {
    return this.http.post<BookingDto>(`${this.base}/validate-qr`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
