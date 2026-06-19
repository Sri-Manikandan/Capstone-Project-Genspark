import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EventDto } from '../../../core/models/event.model';
import { IstDatePipe } from '../../pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-card',
  standalone: true,
  imports: [CommonModule, RouterLink, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a [routerLink]="['/events', event.slug]"
       class="block overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm transition hover:shadow-md">
      <img [src]="event.imageUrl" [alt]="event.title" class="aspect-video w-full object-cover" />
      <div class="space-y-1 p-4">
        <span class="inline-block rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700">
          {{ event.category }}
        </span>
        <h3 class="font-semibold text-gray-900">{{ event.title }}</h3>
        <p class="text-sm text-gray-600">{{ event.startTime | istDate }}</p>
      </div>
    </a>
  `,
})
export class EventCardComponent {
  @Input({ required: true }) event!: EventDto;
}
