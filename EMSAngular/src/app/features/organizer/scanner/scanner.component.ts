import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { AuthService } from '../../../core/services/auth.service';
import { BookingDto } from '../../../core/models/booking.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

@Component({
  selector: 'ems-scanner',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scanner.component.html',
})
export class ScannerComponent {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private bookingService = inject(BookingService);
  private auth = inject(AuthService);

  protected eventId = Number(this.route.snapshot.paramMap.get('id'));
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
