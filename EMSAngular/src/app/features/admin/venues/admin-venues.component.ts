import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { VenueService } from '../../../core/services/venue.service';
import { VenueDto } from '../../../core/models/venue.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';

@Component({
  selector: 'ems-admin-venues',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, LoadingSpinnerComponent, AlertComponent, PaginationComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Venues</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div class="mb-6 flex justify-end">
      <button type="button" (click)="openAdd()" class="btn-primary">+ Add venue</button>
    </div>

    <ul *ngIf="!loading()" class="mb-2 space-y-2">
      <li *ngFor="let v of pagedVenues()" class="flex items-center justify-between gap-4 rounded-xl border border-line bg-surface p-4">
        <span class="text-sm text-ink-soft"><span class="font-display text-base font-semibold text-ink">{{ v.name }}</span> — {{ v.city }} <span class="font-mono text-xs text-muted">(cap {{ v.totalCapacity }})</span></span>
        <span class="flex gap-3">
          <a [routerLink]="['/admin/venues', v.id, 'seats']" class="link-action">Seats</a>
          <button (click)="edit(v)" class="link-action">Edit</button>
          <button (click)="remove(v.id)" class="link-danger">Delete</button>
        </span>
      </li>
      <li *ngIf="venues().length === 0" class="card px-6 py-12 text-center text-muted">No venues yet.</li>
    </ul>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
    <div class="mb-8"></div>

    <ems-modal [open]="dialogOpen()" [title]="editingId() ? 'Edit venue' : 'Add venue'" (closed)="closeDialog()">
      <form [formGroup]="form" (ngSubmit)="save()" class="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <input formControlName="name" placeholder="Name" class="field" />
        <input formControlName="city" placeholder="City" class="field" />
        <input formControlName="address" placeholder="Address" class="field sm:col-span-2" />
        <input formControlName="totalCapacity" type="number" placeholder="Capacity" class="field sm:col-span-2" />
        <div class="flex gap-3 sm:col-span-2">
          <button type="submit" class="btn-primary">{{ editingId() ? 'Save changes' : 'Add venue' }}</button>
          <button type="button" (click)="closeDialog()" class="btn-ghost">Cancel</button>
        </div>
      </form>
    </ems-modal>
  `,
})
export class AdminVenuesComponent implements OnInit {
  private venueService = inject(VenueService);
  private fb = inject(FormBuilder);

  private readonly pageSize = 10;

  protected venues = signal<VenueDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected editingId = signal<number | null>(null);
  protected dialogOpen = signal(false);
  protected page = signal(1);
  protected totalPages = computed(() => Math.max(1, Math.ceil(this.venues().length / this.pageSize)));
  protected pagedVenues = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.venues().slice(start, start + this.pageSize);
  });

  protected goToPage(p: number): void { this.page.set(p); }

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    address: ['', [Validators.required, Validators.minLength(5)]],
    city: ['', [Validators.required, Validators.minLength(2)]],
    totalCapacity: [1, [Validators.min(1)]],
  });

  ngOnInit(): void { this.load(); }

  protected openAdd(): void {
    this.editingId.set(null);
    this.form.reset({ totalCapacity: 1 });
    this.dialogOpen.set(true);
  }

  protected save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const payload = { ...this.form.getRawValue(), layoutConfig: '{}' };
    const id = this.editingId();
    const req$ = id === null
      ? this.venueService.create(payload)
      : this.venueService.update(id, payload);
    req$.subscribe({
      next: () => { this.closeDialog(); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected edit(v: VenueDto): void {
    this.editingId.set(v.id);
    this.form.setValue({ name: v.name, address: v.address, city: v.city, totalCapacity: v.totalCapacity });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void {
    this.dialogOpen.set(false);
    this.editingId.set(null);
  }

  protected remove(id: number): void {
    this.venueService.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.venueService.list().subscribe({
      next: v => {
        this.venues.set(v);
        if (this.page() > this.totalPages()) this.page.set(this.totalPages());
        this.loading.set(false);
      },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
