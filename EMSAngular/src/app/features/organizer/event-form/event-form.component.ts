import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { SeatService } from '../../../core/services/seat.service';
import { VenueDto } from '../../../core/models/venue.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Organizer</p>
    <h1 class="page-title mt-2 mb-6">{{ isEdit() ? 'Edit' : 'Create' }} event</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <form [formGroup]="form" (ngSubmit)="submit()" class="max-w-xl space-y-4">
      <div class="card space-y-4 p-6">
        <label class="block space-y-1" [class.hidden]="isEdit()">
          <span class="field-label">Venue</span>
          <select formControlName="venueId" class="field" (change)="loadScreens(form.controls.venueId.value)">
            <option [ngValue]="0" disabled>Select venue…</option>
            <option *ngFor="let v of venues()" [ngValue]="v.id">{{ v.name }} — {{ v.city }}</option>
          </select>
        </label>
        <label class="block space-y-1">
          <span class="field-label">Screen</span>
          <select formControlName="screen" class="field">
            <option value="">Whole venue (all screens)</option>
            <option *ngFor="let s of screens()" [value]="s">{{ s }}</option>
          </select>
        </label>
        <label class="block space-y-1">
          <span class="field-label">Title</span>
          <input formControlName="title" placeholder="Title" class="field" />
        </label>
        <label class="block space-y-1">
          <span class="field-label">Description</span>
          <textarea formControlName="description" placeholder="Description" rows="4" class="field"></textarea>
        </label>
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <label class="block space-y-1">
            <span class="field-label">Start time</span>
            <input formControlName="startTime" type="datetime-local" class="field" />
          </label>
          <label class="block space-y-1">
            <span class="field-label">End time</span>
            <input formControlName="endTime" type="datetime-local" class="field" />
          </label>
        </div>
        <label class="block space-y-1">
          <span class="field-label">Image URL</span>
          <input formControlName="imageUrl" placeholder="Image URL" class="field" />
        </label>
        <label class="block space-y-1">
          <span class="field-label">Category</span>
          <input formControlName="category" placeholder="Category" class="field" />
        </label>
      </div>
      <button type="submit" class="btn-primary">{{ isEdit() ? 'Save changes' : 'Create event' }}</button>
    </form>
  `,
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
  private eventId = signal<number | null>(null);
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
