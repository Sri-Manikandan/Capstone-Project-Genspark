import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { VenueService } from '../../../core/services/venue.service';
import { VenueDto } from '../../../core/models/venue.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-venues',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Venues</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <ul *ngIf="!loading()" class="mb-6 space-y-2">
      <li *ngFor="let v of venues()" class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-3">
        <span><span class="font-medium text-gray-900">{{ v.name }}</span> — {{ v.city }} (cap {{ v.totalCapacity }})</span>
        <span class="flex gap-3">
          <a [routerLink]="['/admin/venues', v.id, 'seats']" class="text-indigo-600 hover:underline">Seats</a>
          <button (click)="remove(v.id)" class="text-red-600 hover:underline">Delete</button>
        </span>
      </li>
      <li *ngIf="venues().length === 0" class="text-gray-500">No venues yet.</li>
    </ul>

    <form [formGroup]="form" (ngSubmit)="add()" class="grid max-w-xl grid-cols-2 gap-3">
      <input formControlName="name" placeholder="Name" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="city" placeholder="City" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="address" placeholder="Address" class="col-span-2 rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="totalCapacity" type="number" placeholder="Capacity" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="layoutConfig" placeholder="Layout config" class="rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="col-span-2 rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Add venue</button>
    </form>
  `,
})
export class AdminVenuesComponent implements OnInit {
  private venueService = inject(VenueService);
  private fb = inject(FormBuilder);

  protected venues = signal<VenueDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    address: ['', [Validators.required, Validators.minLength(5)]],
    city: ['', [Validators.required, Validators.minLength(2)]],
    totalCapacity: [1, [Validators.min(1)]],
    layoutConfig: ['{}', Validators.required],
  });

  ngOnInit(): void { this.load(); }

  protected add(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.venueService.create(this.form.getRawValue()).subscribe({
      next: () => { this.form.reset({ totalCapacity: 1, layoutConfig: '{}' }); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.venueService.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.venueService.list().subscribe({
      next: v => { this.venues.set(v); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
