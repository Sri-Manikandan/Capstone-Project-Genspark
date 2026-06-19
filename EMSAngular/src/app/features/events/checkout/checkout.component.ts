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
  template: `
    <ems-loading-spinner *ngIf="loading()" />
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <div *ngIf="booking() as b" class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <section class="rounded-lg border border-gray-200 bg-white p-6">
        <h2 class="mb-3 text-lg font-semibold text-gray-900">Order summary</h2>
        <p class="font-medium text-gray-900">{{ b.eventTitle }}</p>
        <ul class="mt-3 space-y-1 text-sm text-gray-600">
          <li *ngFor="let item of b.items" class="flex justify-between">
            <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
            <span>{{ item.unitPrice | inr }}</span>
          </li>
        </ul>
        <div class="mt-3 flex justify-between border-t border-gray-200 pt-3 font-semibold text-gray-900">
          <span>Total</span><span>{{ b.totalAmount | inr }}</span>
        </div>
      </section>

      <section class="rounded-lg border border-gray-200 bg-white p-6">
        <h2 class="mb-3 text-lg font-semibold text-gray-900">Payment</h2>
        <ems-stripe-payment *ngIf="clientSecret()" [clientSecret]="clientSecret()!"
                            (paymentSucceeded)="onPaymentSucceeded($event)"
                            (paymentFailed)="error.set($event)" />
      </section>
    </div>
  `,
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

  ngOnInit(): void {
    const bookingId = Number(this.route.snapshot.paramMap.get('bookingId'));
    this.loading.set(true);
    this.bookingService.getById(bookingId).subscribe({
      next: b => {
        this.booking.set(b);
        this.loading.set(false);
        this.paymentService.initiate({ bookingId, currency: 'inr' }).subscribe({
          next: p => this.clientSecret.set(p.clientSecret),
          error: (msg: string) => this.error.set(msg),
        });
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected onPaymentSucceeded(intentId: string): void {
    this.paymentService.confirm({ stripePaymentIntentId: intentId }).subscribe({
      next: () => this.router.navigate(['/bookings', this.booking()!.id]),
      error: (msg: string) => this.error.set(msg),
    });
  }
}
