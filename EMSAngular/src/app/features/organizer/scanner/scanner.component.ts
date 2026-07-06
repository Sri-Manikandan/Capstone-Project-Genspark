import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { Html5Qrcode } from 'html5-qrcode';
import { BookingService } from '../../../core/services/booking.service';
import { BookingDto } from '../../../core/models/booking.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

const QR_READER_ELEMENT_ID = 'qr-reader';
const RESCAN_GUARD_MS = 3000;

@Component({
  selector: 'ems-scanner',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scanner.component.html',
})
export class ScannerComponent implements OnDestroy {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private bookingService = inject(BookingService);

  protected readonly qrReaderElementId = QR_READER_ELEMENT_ID;
  protected eventId = Number(this.route.snapshot.paramMap.get('id'));
  protected error = signal('');
  protected success = signal('');
  protected result = signal<BookingDto | null>(null);
  protected scanning = signal(false);
  protected form = this.fb.nonNullable.group({ qrPayload: ['', Validators.required] });

  private scanner: Html5Qrcode | null = null;
  private lastScannedPayload = '';
  private lastScannedAt = 0;

  protected async startScan(): Promise<void> {
    if (this.scanning()) return;
    this.error.set('');
    this.scanner = new Html5Qrcode(QR_READER_ELEMENT_ID);
    try {
      await this.scanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        decodedText => this.onScanned(decodedText),
        () => {},
      );
      this.scanning.set(true);
    } catch {
      this.error.set('Could not access camera. Check permissions or use manual entry below.');
      this.scanner = null;
    }
  }

  protected async stopScan(): Promise<void> {
    const scanner = this.scanner;
    if (!scanner) return;
    this.scanner = null;
    this.scanning.set(false);
    try {
      await scanner.stop();
      scanner.clear();
    } catch {
      // scanner was already stopped
    }
  }

  private onScanned(decodedText: string): void {
    const now = Date.now();
    if (decodedText === this.lastScannedPayload && now - this.lastScannedAt < RESCAN_GUARD_MS) return;
    this.lastScannedPayload = decodedText;
    this.lastScannedAt = now;

    this.scanner?.pause(true);
    this.form.setValue({ qrPayload: decodedText });
    this.validate(() => this.scanner?.resume());
  }

  protected validate(onDone?: () => void): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.error.set(''); this.success.set('');
    this.bookingService.validateQr({ qrPayload: this.form.getRawValue().qrPayload })
      .pipe(finalize(() => onDone?.()))
      .subscribe({
        next: b => { this.result.set(b); this.success.set('Ticket valid — marked attended.'); },
        error: (m: string) => { this.error.set(m); this.result.set(null); },
      });
  }

  ngOnDestroy(): void {
    this.stopScan();
  }
}
