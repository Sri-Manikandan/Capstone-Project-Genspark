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

    <div *ngIf="booking() as b">
      <p class="eyebrow text-plum">Final step</p>
      <h1 class="page-title mt-2 mb-7">Checkout</h1>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <section class="card p-6">
          <h2 class="eyebrow mb-4">Order summary</h2>
          <p class="font-display text-lg font-semibold text-ink">{{ b.eventTitle }}</p>
          <ul class="mt-4 space-y-2 text-sm text-ink-soft">
            <li *ngFor="let item of b.items" class="flex justify-between gap-4">
              <span>{{ item.ticketTypeName }} · {{ item.seatLabel }}</span>
              <span class="font-mono">{{ item.unitPrice | inr }}</span>
            </li>
          </ul>
          <div class="mt-5 flex items-center justify-between border-t border-dashed border-line pt-5">
            <span class="eyebrow">Total due</span>
            <span class="font-display text-2xl font-semibold text-ink">{{ b.totalAmount | inr }}</span>
          </div>
        </section>

        <section class="card p-6">
          <h2 class="eyebrow mb-4">Payment</h2>
          <ems-stripe-payment *ngIf="clientSecret()" [clientSecret]="clientSecret()!"
                              (paymentSucceeded)="onPaymentSucceeded($event)"
                              (paymentFailed)="error.set($event)" />
        </section>
      </div>
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
