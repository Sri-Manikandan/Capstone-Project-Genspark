import { Routes } from '@angular/router';
import { EventListComponent } from './event-list/event-list.component';
import { EventDetailComponent } from './event-detail/event-detail.component';

export const EVENTS_ROUTES: Routes = [
  { path: '', component: EventListComponent },
  { path: ':slug', component: EventDetailComponent },
];
