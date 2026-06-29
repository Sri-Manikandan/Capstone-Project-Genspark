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
  templateUrl: './event-card.component.html',
})
export class EventCardComponent {
  @Input({ required: true }) event!: EventDto;
}
