import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ScreeningDto, CreateScreeningRequest, UpdateScreeningRequest,
} from '../models/screening.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class ScreeningService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Screening`;

  getByEvent(eventId: number): Observable<ScreeningDto[]> {
    return this.http.get<ScreeningDto[]>(`${this.base}/event/${eventId}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getById(id: number): Observable<ScreeningDto> {
    return this.http.get<ScreeningDto>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  create(req: CreateScreeningRequest): Observable<ScreeningDto> {
    return this.http.post<ScreeningDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  update(id: number, req: UpdateScreeningRequest): Observable<ScreeningDto> {
    return this.http.put<ScreeningDto>(`${this.base}/${id}`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  subscribeToNotifications(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/${id}/notifications/subscribe`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
