import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PaymentDto, PaymentInitiateDto, InitiatePaymentRequest, ConfirmPaymentRequest,
} from '../models/payment.model';
import { extractError } from './http-error';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/v1/Payment`;

  initiate(req: InitiatePaymentRequest): Observable<PaymentInitiateDto> {
    return this.http.post<PaymentInitiateDto>(`${this.base}/initiate`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  confirm(req: ConfirmPaymentRequest): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(`${this.base}/confirm`, req)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }

  getByBooking(bookingId: number): Observable<PaymentDto> {
    return this.http.get<PaymentDto>(`${this.base}/booking/${bookingId}`)
      .pipe(catchError(e => throwError(() => extractError(e))));
  }
}
