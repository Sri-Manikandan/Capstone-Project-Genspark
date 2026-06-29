import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { SeatService } from '../../../core/services/seat.service';
import { VenueDto } from '../../../core/models/venue.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { RouterLink } from '@angular/router';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

@Component({
  selector: 'ems-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent, RouterLink, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-form.component.html',
})
export class EventFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);
  private venueService = inject(VenueService);
  private seatService = inject(SeatService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected venues = signal<VenueDto[]>([]);
  protected screens = signal<string[]>([]);
  protected error = signal('');
  protected eventId = signal<number | null>(null);
  protected isEdit = computed(() => this.eventId() !== null);

  form = this.fb.nonNullable.group({
    venueId: [0, [Validators.min(1)]],
    screen: [''],
    title: ['', [Validators.required, Validators.minLength(2)]],
    description: ['', [Validators.required, Validators.minLength(1)]],
    startTime: ['', Validators.required],
    endTime: ['', Validators.required],
    imageUrl: ['', [Validators.required]],
    category: ['', [Validators.required, Validators.minLength(2)]],
  });

  ngOnInit(): void {
    this.venueService.list().subscribe({ next: v => this.venues.set(v), error: (m: string) => this.error.set(m) });
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.eventId.set(Number(idParam));
      this.eventService.getById(Number(idParam)).subscribe({
        next: ev => {
          this.form.patchValue({
            venueId: ev.venueId, title: ev.title, description: ev.description,
            startTime: ev.startTime.slice(0, 16), endTime: ev.endTime.slice(0, 16),
            imageUrl: ev.imageUrl, category: ev.category, screen: ev.screen,
          });
          this.loadScreens(ev.venueId);
        },
        error: (m: string) => this.error.set(m),
      });
    }
  }

  protected loadScreens(venueId: number): void {
    if (!venueId) { this.screens.set([]); return; }
    this.seatService.getByVenue(venueId).subscribe({
      next: seats => this.screens.set([...new Set(seats.map(s => s.section))].sort()),
      error: () => this.screens.set([]),
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const id = this.eventId();
    const done = { next: () => this.router.navigate(['/organizer/events']), error: (m: string) => this.error.set(m) };
    if (id !== null) {
      this.eventService.update(id, {
        title: v.title, description: v.description, startTime: v.startTime,
        endTime: v.endTime, imageUrl: v.imageUrl, category: v.category, screen: v.screen,
      }).subscribe(done);
    } else {
      this.eventService.create(v).subscribe(done);
    }
  }
}
