import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { VenueService } from '../../../core/services/venue.service';
import { ToastService } from '../../../core/services/toast.service';
import { VenueDto } from '../../../core/models/venue.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { notBlank } from '../../../shared/validators/form-validators';

@Component({
  selector: 'ems-admin-venues',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, LoadingSpinnerComponent, FieldErrorComponent, PaginationComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-venues.component.html',
})
export class AdminVenuesComponent implements OnInit {
  private venueService = inject(VenueService);
  private toast = inject(ToastService);
  private fb = inject(FormBuilder);

  private readonly pageSize = 10;

  protected venues = signal<VenueDto[]>([]);
  protected loading = signal(false);
  protected saving = signal(false);
  protected editingId = signal<number | null>(null);
  protected dialogOpen = signal(false);
  protected page = signal(1);

  // Preserved across an edit so we never overwrite the venue's seat-map layout — the
  // form has no layout editor, so we send back exactly what we loaded.
  private editingLayout = '{}';
  protected totalPages = computed(() => Math.max(1, Math.ceil(this.venues().length / this.pageSize)));
  protected pagedVenues = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.venues().slice(start, start + this.pageSize);
  });

  protected goToPage(p: number): void { this.page.set(p); }

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, notBlank, Validators.minLength(2), Validators.maxLength(100)]],
    address: ['', [Validators.required, notBlank, Validators.minLength(5), Validators.maxLength(500)]],
    city: ['', [Validators.required, notBlank, Validators.minLength(2), Validators.maxLength(100)]],
    totalCapacity: [1, [Validators.required, Validators.min(1), Validators.max(100000)]],
  });

  ngOnInit(): void { this.load(); }

  protected openAdd(): void {
    this.editingId.set(null);
    this.editingLayout = '{}';
    this.form.reset({ totalCapacity: 1 });
    this.dialogOpen.set(true);
  }

  protected save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    if (this.saving()) return;
    this.saving.set(true);
    const id = this.editingId();
    const payload = { ...this.form.getRawValue(), layoutConfig: id === null ? '{}' : this.editingLayout };
    const req$ = id === null
      ? this.venueService.create(payload)
      : this.venueService.update(id, payload);
    req$.subscribe({
      next: () => { this.saving.set(false); this.toast.success(id === null ? 'Venue created.' : 'Venue updated.'); this.closeDialog(); this.load(); },
      error: (m: string) => { this.saving.set(false); this.toast.error(m); },
    });
  }

  protected edit(v: VenueDto): void {
    this.editingId.set(v.id);
    this.editingLayout = v.layoutConfig ?? '{}';
    this.form.setValue({ name: v.name, address: v.address, city: v.city, totalCapacity: v.totalCapacity });
    this.dialogOpen.set(true);
  }

  protected closeDialog(): void {
    this.dialogOpen.set(false);
    this.editingId.set(null);
  }

  protected remove(id: number): void {
    if (!confirm('Delete this venue? This cannot be undone.')) return;
    this.venueService.delete(id).subscribe({
      next: () => { this.toast.success('Venue deleted.'); this.load(); },
      error: (m: string) => this.toast.error(m),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.venueService.list().subscribe({
      next: v => {
        this.venues.set(v);
        if (this.page() > this.totalPages()) this.page.set(this.totalPages());
        this.loading.set(false);
      },
      error: (m: string) => { this.toast.error(m); this.loading.set(false); },
    });
  }
}
