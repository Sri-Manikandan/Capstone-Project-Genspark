import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { VenueDto, CreateVenueRequest, UpdateVenueRequest } from '../models/venue.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class VenueService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Venue`;

  list(): Observable<VenueDto[]> {
    return this.http.get<VenueDto[]>(this.base)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getById(id: number): Observable<VenueDto> {
    return this.http.get<VenueDto>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  create(req: CreateVenueRequest): Observable<VenueDto> {
    return this.http.post<VenueDto>(this.base, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  update(id: number, req: UpdateVenueRequest): Observable<VenueDto> {
    return this.http.put<VenueDto>(`${this.base}/${id}`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
