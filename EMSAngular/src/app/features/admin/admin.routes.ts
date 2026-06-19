import { Routes } from '@angular/router';
import { EventApprovalsComponent } from './event-approvals/event-approvals.component';
import { OrganizerRequestsComponent } from './organizer-requests/organizer-requests.component';
import { AdminUsersComponent } from './users/admin-users.component';
import { AdminVenuesComponent } from './venues/admin-venues.component';
import { AdminSeatsComponent } from './seats/admin-seats.component';

export const ADMIN_ROUTES: Routes = [
  { path: 'events', component: EventApprovalsComponent },
  { path: 'organizer-requests', component: OrganizerRequestsComponent },
  { path: 'users', component: AdminUsersComponent },
  { path: 'venues', component: AdminVenuesComponent },
  { path: 'venues/:id/seats', component: AdminSeatsComponent },
  { path: '', pathMatch: 'full', redirectTo: 'events' },
];
