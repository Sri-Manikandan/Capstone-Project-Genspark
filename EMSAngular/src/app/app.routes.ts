import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent),
  },
  {
    path: 'events',
    loadChildren: () => import('./features/events/events.routes').then(m => m.EVENTS_ROUTES),
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES),
  },
  {
    path: 'bookings',
    canActivate: [authGuard],
    loadChildren: () => import('./features/bookings/bookings.routes').then(m => m.BOOKINGS_ROUTES),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadChildren: () => import('./features/profile/profile.routes').then(m => m.PROFILE_ROUTES),
  },
  {
    path: 'checkout',
    canActivate: [authGuard],
    loadChildren: () => import('./features/events/checkout.routes').then(m => m.CHECKOUT_ROUTES),
  },
  {
    path: 'organizer',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Organizer', 'Admin'] },
    loadChildren: () => import('./features/organizer/organizer.routes').then(m => m.ORGANIZER_ROUTES),
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
    loadChildren: () => import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
  },
  { path: '**', redirectTo: 'events' },
];
