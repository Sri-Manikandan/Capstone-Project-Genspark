import { Routes } from '@angular/router';
import { AdminLayoutComponent } from './admin-layout.component';
import { EventApprovalsComponent } from './event-approvals/event-approvals.component';
import { OrganizerRequestsComponent } from './organizer-requests/organizer-requests.component';
import { AdminUsersComponent } from './users/admin-users.component';
import { AdminVenuesComponent } from './venues/admin-venues.component';
import { AdminSeatsComponent } from './seats/admin-seats.component';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    children: [
      { path: 'events', component: EventApprovalsComponent },
      { path: 'organizer-requests', component: OrganizerRequestsComponent },
      { path: 'users', component: AdminUsersComponent },
      { path: 'venues', component: AdminVenuesComponent },
      { path: 'venues/:id/seats', component: AdminSeatsComponent },
      { path: '', pathMatch: 'full', redirectTo: 'events' },
    ],
  },
];
