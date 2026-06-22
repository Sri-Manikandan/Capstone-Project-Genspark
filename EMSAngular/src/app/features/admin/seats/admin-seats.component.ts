import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SeatService } from '../../../core/services/seat.service';
import { SeatDto } from '../../../core/models/seat.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-seats',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Seats</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <form [formGroup]="form" (ngSubmit)="bulkCreate()" class="card mb-8 grid max-w-xl grid-cols-1 gap-3 p-6 sm:grid-cols-2">
      <input formControlName="section" placeholder="Section" class="field" />
      <input formControlName="row" placeholder="Row" class="field" />
      <input formControlName="startNumber" type="number" placeholder="Start #" class="field" />
      <input formControlName="endNumber" type="number" placeholder="End #" class="field" />
      <input formControlName="seatType" placeholder="Seat type" class="field sm:col-span-2" />
      <button type="submit" class="btn-primary sm:col-span-2">Bulk create row</button>
    </form>

    <div *ngIf="!loading()" class="flex flex-wrap gap-2">
      <span *ngFor="let s of seats()" class="flex items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 py-1 font-mono text-xs text-ink-soft">
        {{ s.section }}-{{ s.row }}-{{ s.seatNumber }}
        <button (click)="remove(s.id)" class="text-rose transition hover:text-rose-dark" aria-label="Remove seat">×</button>
      </span>
      <p *ngIf="seats().length === 0" class="text-muted">No seats yet.</p>
    </div>
  `,
})
export class AdminSeatsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private seatService = inject(SeatService);
  private fb = inject(FormBuilder);

  protected seats = signal<SeatDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  private venueId = Number(this.route.snapshot.paramMap.get('id'));

  protected form = this.fb.nonNullable.group({
    section: ['', [Validators.required, Validators.minLength(1)]],
    row: ['', [Validators.required, Validators.minLength(1)]],
    startNumber: [1, [Validators.min(1)]],
    endNumber: [1, [Validators.min(1)]],
    seatType: ['', [Validators.required, Validators.minLength(2)]],
  });

  ngOnInit(): void { this.load(); }

  protected bulkCreate(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.seatService.bulkCreate({ venueId: this.venueId, ...this.form.getRawValue() }).subscribe({
      next: () => { this.form.reset({ startNumber: 1, endNumber: 1 }); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.seatService.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.seatService.getByVenue(this.venueId).subscribe({
      next: s => { this.seats.set(s); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
