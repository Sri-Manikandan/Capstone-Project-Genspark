import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StripePaymentComponent } from './stripe-payment.component';

describe('StripePaymentComponent', () => {
  let fixture: ComponentFixture<StripePaymentComponent>;
  let component: StripePaymentComponent;

  const stripeStub = {
    elements: () => ({ create: () => ({ mount: () => {} }) }),
    confirmPayment: vi.fn().mockResolvedValue({ paymentIntent: { id: 'pi_123', status: 'succeeded' } }),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [StripePaymentComponent] });
    fixture = TestBed.createComponent(StripePaymentComponent);
    component = fixture.componentInstance;
    component['loadStripeFn'] = (() => Promise.resolve(stripeStub)) as any;
    fixture.componentRef.setInput('clientSecret', 'cs_test_123');
  });

  it('emits paymentSucceeded with the intent id on success', async () => {
    let emitted = '';
    component.paymentSucceeded.subscribe((id: string) => (emitted = id));
    await fixture.whenStable();
    component['stripe'] = stripeStub as any;
    component['elements'] = stripeStub.elements() as any;
    await component['pay']();
    expect(emitted).toBe('pi_123');
  });

  it('emits paymentFailed when confirmPayment returns an error', async () => {
    let failed = '';
    component.paymentFailed.subscribe((m: string) => (failed = m));
    component['stripe'] = { confirmPayment: () => Promise.resolve({ error: { message: 'Card declined' } }) } as any;
    component['elements'] = {} as any;
    await component['pay']();
    expect(failed).toBe('Card declined');
    expect(component['errorMessage']()).toBe('Card declined');
  });
});
