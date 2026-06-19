import { Routes } from '@angular/router';
import { OrganizerEventListComponent } from './event-list/organizer-event-list.component';
import { EventFormComponent } from './event-form/event-form.component';
import { TicketTypesComponent } from './ticket-types/ticket-types.component';
import { ScannerComponent } from './scanner/scanner.component';

export const ORGANIZER_ROUTES: Routes = [
  { path: 'events', component: OrganizerEventListComponent },
  { path: 'events/new', component: EventFormComponent },
  { path: 'events/:id/edit', component: EventFormComponent },
  { path: 'events/:id/tickets', component: TicketTypesComponent },
  { path: 'events/:id/bookings', component: ScannerComponent },
  { path: '', pathMatch: 'full', redirectTo: 'events' },
];
