import { Routes } from '@angular/router';
import { CheckoutComponent } from './checkout/checkout.component';

export const CHECKOUT_ROUTES: Routes = [
  { path: ':bookingId', component: CheckoutComponent },
];
