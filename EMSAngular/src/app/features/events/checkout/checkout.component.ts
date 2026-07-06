import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';
import { BookingDto } from '../../../core/models/booking.model';
import { StripePaymentComponent } from '../../../shared/components/stripe-payment/stripe-payment.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-checkout',
  standalone: true,
  imports: [CommonModule, StripePaymentComponent, LoadingSpinnerComponent, AlertComponent, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout.component.html',
})
export class CheckoutComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private bookingService = inject(BookingService);
  private paymentService = inject(PaymentService);

  protected booking = signal<BookingDto | null>(null);
  protected clientSecret = signal<string | null>(null);
  protected loading = signal(false);
  protected error = signal('');

  private bookingId = 0;

  ngOnInit(): void {
    this.bookingId = Number(this.route.snapshot.paramMap.get('bookingId'));

    // When a payment method requires a redirect (e.g. 3D Secure), Stripe sends the
    // browser back here with the intent id in the query string. Confirm that intent
    // instead of starting a brand-new payment, otherwise the booking stays Pending.
    const intentId = this.route.snapshot.queryParamMap.get('payment_intent');
    const redirectStatus = this.route.snapshot.queryParamMap.get('redirect_status');
    if (intentId && redirectStatus === 'succeeded') {
      this.confirmPayment(intentId);
      return;
    }
    if (intentId) {
      this.error.set('Payment was not completed. Please try again.');
    }

    this.loadBookingAndInitiate();
  }

  private loadBookingAndInitiate(): void {
    this.loading.set(true);
    this.bookingService.getById(this.bookingId).subscribe({
      next: b => {
        this.booking.set(b);
        this.loading.set(false);
        this.paymentService.initiate({ bookingId: this.bookingId, currency: 'inr' }).subscribe({
          next: p => this.clientSecret.set(p.clientSecret),
          error: (msg: string) => this.error.set(msg),
        });
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected onPaymentSucceeded(intentId: string): void {
    this.confirmPayment(intentId);
  }

  private confirmPayment(intentId: string): void {
    this.paymentService.confirm({ stripePaymentIntentId: intentId }).subscribe({
      next: () => this.router.navigate(['/bookings', this.bookingId], { queryParams: { confirmed: 1 } }),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
