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
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Seats</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <form [formGroup]="form" (ngSubmit)="bulkCreate()" class="mb-6 grid max-w-xl grid-cols-2 gap-3">
      <input formControlName="section" placeholder="Section" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="row" placeholder="Row" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="startNumber" type="number" placeholder="Start #" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="endNumber" type="number" placeholder="End #" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="seatType" placeholder="Seat type" class="col-span-2 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="col-span-2 rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Bulk create row</button>
    </form>

    <div *ngIf="!loading()" class="flex flex-wrap gap-2">
      <span *ngFor="let s of seats()" class="flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-2 py-1 text-sm">
        {{ s.section }}-{{ s.row }}-{{ s.seatNumber }}
        <button (click)="remove(s.id)" class="text-red-600">×</button>
      </span>
      <p *ngIf="seats().length === 0" class="text-gray-500">No seats yet.</p>
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
