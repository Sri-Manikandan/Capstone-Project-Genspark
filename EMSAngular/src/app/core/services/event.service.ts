import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import {
  EventDto, CreateEventRequest, UpdateEventRequest, EventSearchRequest,
} from '../models/event.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class EventService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Event`;

  search(req: EventSearchRequest): Observable<PagedResult<EventDto>> {
    return this.http.get<PagedResult<EventDto>>(this.base, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/categories`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getCities(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/cities`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getById(id: number): Observable<EventDto> {
    return this.http.get<EventDto>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getBySlug(slug: string): Observable<EventDto> {
    return this.http.get<EventDto>(`${this.base}/slug/${slug}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getMyEvents(page: number, pageSize: number): Observable<PagedResult<EventDto>> {
    return this.http.get<PagedResult<EventDto>>(`${this.base}/my`, { params: toHttpParams({ page, pageSize }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  create(req: CreateEventRequest): Observable<EventDto> {
    return this.http.post<EventDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  update(id: number, req: UpdateEventRequest): Observable<EventDto> {
    return this.http.put<EventDto>(`${this.base}/${id}`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  submit(id: number): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/${id}/submit`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  cancel(id: number): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/${id}/cancel`, {})
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
