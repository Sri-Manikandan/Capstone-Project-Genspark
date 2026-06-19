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
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Validate Ticket</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-alert type="success" [message]="success()" (dismissed)="success.set('')" />

    <form [formGroup]="form" (ngSubmit)="validate()" class="flex max-w-xl gap-3">
      <input formControlName="qrPayload" placeholder="Paste QR payload" class="flex-1 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Validate</button>
    </form>

    <div *ngIf="result() as b" class="mt-4 rounded-lg border border-gray-200 bg-white p-4">
      <p class="font-medium text-gray-900">{{ b.eventTitle }} — {{ b.bookingReference }}</p>
      <p class="text-sm text-gray-600">Status: {{ b.bookingStatus }}</p>
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
