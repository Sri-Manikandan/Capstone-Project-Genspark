import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';
import { VenueDto } from '../../../core/models/venue.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">{{ isEdit() ? 'Edit' : 'Create' }} Event</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <form [formGroup]="form" (ngSubmit)="submit()" class="max-w-xl space-y-4">
      <select formControlName="venueId" class="w-full rounded-lg border border-gray-300 px-3 py-2" [class.hidden]="isEdit()">
        <option [ngValue]="0" disabled>Select venue…</option>
        <option *ngFor="let v of venues()" [ngValue]="v.id">{{ v.name }} — {{ v.city }}</option>
      </select>
      <input formControlName="title" placeholder="Title" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
      <textarea formControlName="description" placeholder="Description" rows="4" class="w-full rounded-lg border border-gray-300 px-3 py-2"></textarea>
      <label class="block text-sm text-gray-600">Start time
        <input formControlName="startTime" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" />
      </label>
      <label class="block text-sm text-gray-600">End time
        <input formControlName="endTime" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" />
      </label>
      <input formControlName="imageUrl" placeholder="Image URL" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="category" placeholder="Category" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-5 py-2 text-white hover:bg-indigo-700">{{ isEdit() ? 'Save' : 'Create' }}</button>
    </form>
  `,
})
export class EventFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);
  private venueService = inject(VenueService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected venues = signal<VenueDto[]>([]);
  protected error = signal('');
  private eventId = signal<number | null>(null);
  protected isEdit = computed(() => this.eventId() !== null);

  form = this.fb.nonNullable.group({
    venueId: [0, [Validators.min(1)]],
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
        next: ev => this.form.patchValue({
          venueId: ev.venueId, title: ev.title, description: ev.description,
          startTime: ev.startTime.slice(0, 16), endTime: ev.endTime.slice(0, 16),
          imageUrl: ev.imageUrl, category: ev.category,
        }),
        error: (m: string) => this.error.set(m),
      });
    }
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const id = this.eventId();
    const done = { next: () => this.router.navigate(['/organizer/events']), error: (m: string) => this.error.set(m) };
    if (id !== null) {
      this.eventService.update(id, {
        title: v.title, description: v.description, startTime: v.startTime,
        endTime: v.endTime, imageUrl: v.imageUrl, category: v.category,
      }).subscribe(done);
    } else {
      this.eventService.create(v).subscribe(done);
    }
  }
}
