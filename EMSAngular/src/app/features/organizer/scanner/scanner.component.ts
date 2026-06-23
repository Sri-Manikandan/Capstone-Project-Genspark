import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { BookingDto } from '../../../core/models/booking.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-scanner',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">At the door</p>
    <h1 class="page-title mt-2 mb-6">Validate ticket</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-alert type="success" [message]="success()" (dismissed)="success.set('')" />

    <form [formGroup]="form" (ngSubmit)="validate()" class="flex max-w-xl flex-col gap-3 sm:flex-row">
      <input formControlName="qrPayload" aria-label="QR payload" placeholder="Paste QR payload" class="field flex-1" />
      <button type="submit" class="btn-primary">Validate</button>
    </form>

    <div *ngIf="result() as b" class="card mt-5 max-w-xl p-5">
      <p class="font-display text-lg font-semibold text-ink">{{ b.eventTitle }}</p>
      <p class="font-mono text-xs text-muted">{{ b.bookingReference }}</p>
      <p class="mt-2 text-sm text-ink-soft">Status: <span class="font-semibold text-teal-dark">{{ b.bookingStatus }}</span></p>
    </div>
  `,
})
export class ScannerComponent {
  private fb = inject(FormBuilder);
  private bookingService = inject(BookingService);
  private auth = inject(AuthService);

  protected error = signal('');
  protected success = signal('');
  protected result = signal<BookingDto | null>(null);
  protected form = this.fb.nonNullable.group({ qrPayload: ['', Validators.required] });

  protected validate(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.error.set(''); this.success.set('');
    const scannedBy = this.auth.currentUser()?.id ?? 0;
    this.bookingService.validateQr({ qrPayload: this.form.getRawValue().qrPayload, scannedBy }).subscribe({
      next: b => { this.result.set(b); this.success.set('Ticket valid — marked attended.'); },
      error: (m: string) => { this.error.set(m); this.result.set(null); },
    });
  }
}
