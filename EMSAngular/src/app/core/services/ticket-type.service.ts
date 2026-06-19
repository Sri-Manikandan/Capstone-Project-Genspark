import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TicketTypeDto, CreateTicketTypeRequest, UpdateTicketTypeRequest,
} from '../models/ticket-type.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class TicketTypeService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/TicketType`;

  getByEvent(eventId: number): Observable<TicketTypeDto[]> {
    return this.http.get<TicketTypeDto[]>(`${this.base}/event/${eventId}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getActiveByEvent(eventId: number): Observable<TicketTypeDto[]> {
    return this.http.get<TicketTypeDto[]>(`${this.base}/event/${eventId}/active`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getById(id: number): Observable<TicketTypeDto> {
    return this.http.get<TicketTypeDto>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  create(req: CreateTicketTypeRequest): Observable<TicketTypeDto> {
    return this.http.post<TicketTypeDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  update(id: number, req: UpdateTicketTypeRequest): Observable<TicketTypeDto> {
    return this.http.put<TicketTypeDto>(`${this.base}/${id}`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
