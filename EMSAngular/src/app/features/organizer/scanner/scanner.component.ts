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

    // Camera access needs a secure context; over plain http (e.g. a LAN IP on a laptop)
    // navigator.mediaDevices is undefined, so fail early with an actionable message.
    if (!navigator.mediaDevices?.getUserMedia) {
      this.error.set('Camera needs a secure (HTTPS) connection. Open the site over HTTPS or use manual entry below.');
      return;
    }

    // Reveal the reader first: html5-qrcode sizes the video from the container, and a
    // display:none (zero-width) element breaks the camera preview.
    this.scanning.set(true);
    const scanner = new Html5Qrcode(QR_READER_ELEMENT_ID);
    this.scanner = scanner;
    try {
      const cameraId = await this.pickCamera();
      await scanner.start(
        cameraId,
        { fps: 10, qrbox: { width: 250, height: 250 } },
        decodedText => this.onScanned(decodedText),
        () => {},
      );
    } catch (err) {
      this.scanner = null;
      this.scanning.set(false);
      this.error.set(this.cameraErrorMessage(err));
    }
  }

  // Laptops typically expose only a front-facing webcam, so prefer a rear/back camera
  // when the device has one but fall back to whatever is available instead of forcing
  // facingMode 'environment' (which leaves laptops with no usable camera).
  private async pickCamera(): Promise<string> {
    const cameras = await Html5Qrcode.getCameras();
    if (cameras.length === 0) throw new Error('NO_CAMERA');
    const rear = cameras.find(c => /\b(back|rear|environment)\b/i.test(c.label));
    return (rear ?? cameras[0]).id;
  }

  private cameraErrorMessage(err: unknown): string {
    const name = (err as { name?: string })?.name ?? '';
    const message = (err as { message?: string })?.message ?? '';
    if (name === 'NotAllowedError' || name === 'SecurityError')
      return 'Camera permission was blocked. Allow camera access in your browser, then try again — or use manual entry below.';
    if (name === 'NotFoundError' || message === 'NO_CAMERA')
      return 'No camera was found on this device. Use manual entry below.';
    if (name === 'NotReadableError')
      return 'The camera is in use by another app. Close it and try again, or use manual entry below.';
    return 'Could not access camera. Check permissions or use manual entry below.';
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
