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
  templateUrl: './admin-venues.component.html',
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
