import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import {
  User, UpdateUserRequest, ChangePasswordRequest, ChangeEmailRequest,
  CloseAccountRequest, UserSearchRequest,
} from '../models/user.model';
import { OrganizerRequestDto } from '../models/admin.model';
import { extractError, toHttpParams } from './http-error';

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/User`;

  getMe(): Observable<User> {
    return this.http.get<User>(`${this.base}/me`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  updateMe(req: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.base}/me`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  changePassword(req: ChangePasswordRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/me/password`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  changeEmail(req: ChangeEmailRequest): Observable<User> {
    return this.http.put<User>(`${this.base}/me/email`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  deleteMe(req: CloseAccountRequest): Observable<void> {
    return this.http.delete<void>(`${this.base}/me`, { body: req })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  search(req: UserSearchRequest): Observable<PagedResult<User>> {
    return this.http.get<PagedResult<User>>(this.base, { params: toHttpParams({ ...req }) })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getById(id: number): Observable<User> {
    return this.http.get<User>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  deleteById(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  requestOrganizer(reason: string): Observable<OrganizerRequestDto> {
    return this.http.post<OrganizerRequestDto>(`${this.base}/request-organizer`, { reason })
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getOrganizerRequest(): Observable<OrganizerRequestDto> {
    return this.http.get<OrganizerRequestDto>(`${this.base}/organizer-request`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
