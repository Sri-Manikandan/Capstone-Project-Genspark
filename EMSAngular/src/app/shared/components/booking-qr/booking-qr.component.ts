import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'ems-booking-qr',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center gap-2">
      <img [src]="src()" alt="Booking QR code" class="h-48 w-48 rounded-lg border border-gray-200" />
      <a [href]="src()" download="ticket-qr.png"
         class="text-sm text-indigo-600 hover:underline">Download QR</a>
    </div>
  `,
})
export class BookingQrComponent {
  @Input() qrCode = '';

  protected src(): string {
    if (!this.qrCode) return '';
    return this.qrCode.startsWith('data:') ? this.qrCode : `data:image/png;base64,${this.qrCode}`;
  }
}
