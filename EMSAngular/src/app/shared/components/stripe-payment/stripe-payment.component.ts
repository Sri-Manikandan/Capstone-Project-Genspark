import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { loadStripe, Stripe, StripeElements } from '@stripe/stripe-js';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'ems-stripe-payment',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form (ngSubmit)="pay()" class="space-y-4">
      <div id="payment-element"></div>
      <p *ngIf="errorMessage()" class="text-sm text-rose">{{ errorMessage() }}</p>
      <button type="submit" [disabled]="submitting()" class="btn-primary w-full">
        {{ submitting() ? 'Processing…' : 'Pay now' }}
      </button>
    </form>
  `,
})
export class StripePaymentComponent implements AfterViewInit {
  @Input({ required: true }) clientSecret!: string;
  @Output() paymentSucceeded = new EventEmitter<string>();
  @Output() paymentFailed = new EventEmitter<string>();

  protected submitting = signal(false);
  protected errorMessage = signal('');
  protected loadStripeFn = loadStripe;

  private stripe: Stripe | null = null;
  private elements: StripeElements | null = null;

  async ngAfterViewInit(): Promise<void> {
    this.stripe = await this.loadStripeFn(environment.stripePublishableKey);
    if (!this.stripe) {
      this.errorMessage.set('Failed to load payment provider.');
      return;
    }
    this.elements = this.stripe.elements({ clientSecret: this.clientSecret });
    const paymentElement = this.elements.create('payment');
    paymentElement.mount('#payment-element');
  }

  protected async pay(): Promise<void> {
    if (!this.stripe || !this.elements) return;
    this.submitting.set(true);
    this.errorMessage.set('');
    const result = await this.stripe.confirmPayment({
      elements: this.elements,
      redirect: 'if_required',
    });
    this.submitting.set(false);
    if (result.error) {
      const msg = result.error.message ?? 'Payment failed.';
      this.errorMessage.set(msg);
      this.paymentFailed.emit(msg);
      return;
    }
    this.paymentSucceeded.emit(result.paymentIntent!.id);
  }
}
