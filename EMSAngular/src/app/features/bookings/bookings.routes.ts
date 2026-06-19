import { Routes } from '@angular/router';
import { BookingListComponent } from './booking-list/booking-list.component';
import { BookingDetailComponent } from './booking-detail/booking-detail.component';

export const BOOKINGS_ROUTES: Routes = [
  { path: '', component: BookingListComponent },
  { path: ':id', component: BookingDetailComponent },
];
