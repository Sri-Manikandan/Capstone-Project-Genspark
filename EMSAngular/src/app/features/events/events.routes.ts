import { Routes } from '@angular/router';
import { EventListComponent } from './event-list/event-list.component';

export const EVENTS_ROUTES: Routes = [
  { path: '', component: EventListComponent },
];
