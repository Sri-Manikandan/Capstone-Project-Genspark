import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SeatDto, CreateSeatRequest, BulkCreateSeatsRequest, ReserveSeatRequest, SeatReservationDto,
  SetScreenSeatsRequest,
} from '../models/seat.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class SeatService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Seat`;

  getByVenue(venueId: number): Observable<SeatDto[]> {
    return this.http.get<SeatDto[]>(`${this.base}/venue/${venueId}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getAvailableByEvent(eventId: number): Observable<SeatDto[]> {
    return this.http.get<SeatDto[]>(`${this.base}/available/event/${eventId}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  create(req: CreateSeatRequest): Observable<SeatDto> {
    return this.http.post<SeatDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  bulkCreate(req: BulkCreateSeatsRequest): Observable<SeatDto[]> {
    return this.http.post<SeatDto[]>(`${this.base}/bulk`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  reserve(req: ReserveSeatRequest): Observable<SeatReservationDto> {
    return this.http.post<SeatReservationDto>(`${this.base}/reserve`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  releaseReservation(reservationId: number): Observable<void> {
    return this.http.post<void>(`${this.base}/reserve/${reservationId}/release`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  setScreenSeats(req: SetScreenSeatsRequest): Observable<SeatDto[]> {
    return this.http.put<SeatDto[]>(`${this.base}/screen`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
