import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { CheckoutComponent } from './checkout.component';
import { BookingService } from '../../../core/services/booking.service';
import { PaymentService } from '../../../core/services/payment.service';

const booking = {
  id: 123, userId: 1, eventId: 5, eventTitle: 'Show', bookingReference: 'BK1',
  qrCode: '', totalAmount: 200, bookingStatus: 'Pending', expiresAt: '', createdAt: '', items: [],
};

describe('CheckoutComponent', () => {
  let fixture: ComponentFixture<CheckoutComponent>;
  let component: CheckoutComponent;
  let payment: { initiate: ReturnType<typeof vi.fn>; confirm: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    payment = {
      initiate: vi.fn().mockReturnValue(of({ clientSecret: 'cs_1' })),
      confirm: vi.fn().mockReturnValue(of({ id: 1 })),
    };
    TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => '123' }, queryParamMap: { get: () => null } } },
        },
        { provide: BookingService, useValue: { getById: () => of(booking) } },
        { provide: PaymentService, useValue: payment },
      ],
    });
    fixture = TestBed.createComponent(CheckoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads booking and initiates payment', () => {
    expect(component['booking']()?.id).toBe(123);
    expect(payment.initiate).toHaveBeenCalledWith({ bookingId: 123, currency: 'inr' });
    expect(component['clientSecret']()).toBe('cs_1');
  });

  it('confirms payment and navigates to booking on success', () => {
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component['onPaymentSucceeded']('pi_123');
    expect(payment.confirm).toHaveBeenCalledWith({ stripePaymentIntentId: 'pi_123' });
    expect(nav).toHaveBeenCalledWith(['/bookings', 123], { queryParams: { confirmed: 1 } });
  });
});

describe('CheckoutComponent — Stripe redirect return', () => {
  const booking = {
    id: 123, userId: 1, eventId: 5, eventTitle: 'Show', bookingReference: 'BK1',
    qrCode: '', totalAmount: 200, bookingStatus: 'Pending', expiresAt: '', createdAt: '', items: [],
  };

  function setup(query: Record<string, string>) {
    const payment = {
      initiate: vi.fn().mockReturnValue(of({ clientSecret: 'cs_1' })),
      confirm: vi.fn().mockReturnValue(of({ id: 1 })),
    };
    TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => '123' },
              queryParamMap: { get: (k: string) => query[k] ?? null },
            },
          },
        },
        { provide: BookingService, useValue: { getById: () => of(booking) } },
        { provide: PaymentService, useValue: payment },
      ],
    });
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(CheckoutComponent);
    return { fixture, component: fixture.componentInstance, payment, nav };
  }

  it('confirms the returned intent and does not re-initiate on success', () => {
    const { fixture, payment, nav } = setup({ payment_intent: 'pi_redirect', redirect_status: 'succeeded' });
    fixture.detectChanges();
    expect(payment.confirm).toHaveBeenCalledWith({ stripePaymentIntentId: 'pi_redirect' });
    expect(payment.initiate).not.toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/bookings', 123], { queryParams: { confirmed: 1 } });
  });

  it('shows an error and re-initiates when the redirect did not succeed', () => {
    const { fixture, component, payment } = setup({ payment_intent: 'pi_redirect', redirect_status: 'failed' });
    fixture.detectChanges();
    expect(payment.confirm).not.toHaveBeenCalled();
    expect(payment.initiate).toHaveBeenCalledWith({ bookingId: 123, currency: 'inr' });
    expect(component['error']()).not.toBe('');
  });
});
