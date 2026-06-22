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
       class="group relative block rounded-2xl border border-line bg-surface shadow-card transition duration-300 hover:-translate-y-1 hover:shadow-lift focus-visible:-translate-y-1">
      <div class="overflow-hidden rounded-t-2xl bg-gradient-to-br from-plum/10 to-teal/10">
        <img [src]="event.imageUrl" [alt]="event.title"
             class="aspect-[4/3] w-full object-cover transition duration-500 group-hover:scale-[1.04]" />
      </div>
      <div class="p-5">
        <span class="eyebrow text-plum">{{ event.category }}</span>
        <h3 class="mt-2 font-display text-xl font-semibold leading-snug text-ink">{{ event.title }}</h3>
      </div>
      <div class="perf">
        <div class="flex items-center justify-between px-5 py-3">
          <span class="font-mono text-xs text-ink-soft">{{ event.startTime | istDate: 'date' }}</span>
          <span class="eyebrow">Admit one</span>
        </div>
      </div>
    </a>
  `,
})
export class EventCardComponent {
  @Input({ required: true }) event!: EventDto;
}
