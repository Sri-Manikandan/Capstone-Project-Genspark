import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'ems-booking-qr',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './booking-qr.component.html',
})
export class BookingQrComponent {
  @Input() qrCode = '';

  protected src(): string {
    if (!this.qrCode) return '';
    return this.qrCode.startsWith('data:') ? this.qrCode : `data:image/png;base64,${this.qrCode}`;
  }
}
