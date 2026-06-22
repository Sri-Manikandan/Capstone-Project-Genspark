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
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Venues</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <ul *ngIf="!loading()" class="mb-8 space-y-2">
      <li *ngFor="let v of venues()" class="flex items-center justify-between gap-4 rounded-xl border border-line bg-surface p-4">
        <span class="text-sm text-ink-soft"><span class="font-display text-base font-semibold text-ink">{{ v.name }}</span> — {{ v.city }} <span class="font-mono text-xs text-muted">(cap {{ v.totalCapacity }})</span></span>
        <span class="flex gap-3">
          <a [routerLink]="['/admin/venues', v.id, 'seats']" class="link-action">Seats</a>
          <button (click)="remove(v.id)" class="link-danger">Delete</button>
        </span>
      </li>
      <li *ngIf="venues().length === 0" class="card px-6 py-12 text-center text-muted">No venues yet.</li>
    </ul>

    <form [formGroup]="form" (ngSubmit)="add()" class="card grid max-w-xl grid-cols-1 gap-3 p-6 sm:grid-cols-2">
      <input formControlName="name" placeholder="Name" class="field" />
      <input formControlName="city" placeholder="City" class="field" />
      <input formControlName="address" placeholder="Address" class="field sm:col-span-2" />
      <input formControlName="totalCapacity" type="number" placeholder="Capacity" class="field" />
      <input formControlName="layoutConfig" placeholder="Layout config" class="field" />
      <button type="submit" class="btn-primary sm:col-span-2">Add venue</button>
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
