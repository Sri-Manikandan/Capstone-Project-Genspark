import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import { EventDto } from '../models/event.model';
import { User, UserSearchRequest } from '../models/user.model';
import {
  OrganizerRequestDto, ReviewRequest, OrganizerRequestQueryRequest,
} from '../models/admin.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Admin`;

  getOrganizerRequests(req: OrganizerRequestQueryRequest): Observable<PagedResult<OrganizerRequestDto>> {
    return this.http.get<PagedResult<OrganizerRequestDto>>(`${this.base}/organizer-requests`, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  approveOrganizerRequest(id: number, req: ReviewRequest): Observable<OrganizerRequestDto> {
    return this.http.post<OrganizerRequestDto>(`${this.base}/organizer-requests/${id}/approve`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  rejectOrganizerRequest(id: number, req: ReviewRequest): Observable<OrganizerRequestDto> {
    return this.http.post<OrganizerRequestDto>(`${this.base}/organizer-requests/${id}/reject`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getPendingEvents(page: number, pageSize: number): Observable<PagedResult<EventDto>> {
    return this.http.get<PagedResult<EventDto>>(`${this.base}/events/pending`, { params: toHttpParams({ page, pageSize }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  approveEvent(id: number, req: ReviewRequest): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/events/${id}/approve`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  rejectEvent(id: number, req: ReviewRequest): Observable<EventDto> {
    return this.http.post<EventDto>(`${this.base}/events/${id}/reject`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getUsers(req: UserSearchRequest): Observable<PagedResult<User>> {
    return this.http.get<PagedResult<User>>(`${this.base}/users`, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/users/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
