import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-alert',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="message" class="mb-4 flex items-start justify-between gap-3 rounded-xl px-4 py-3 text-sm"
         [class.bg-teal-tint]="type === 'success'" [class.text-teal-dark]="type === 'success'"
         [class.bg-rose-tint]="type === 'error'" [class.text-rose-dark]="type === 'error'"
         [class.bg-plum-tint]="type === 'info'" [class.text-plum-dark]="type === 'info'">
      <span class="font-medium">{{ message }}</span>
      <button class="text-base font-bold leading-none opacity-60 transition hover:opacity-100" (click)="dismissed.emit()" aria-label="Dismiss">×</button>
    </div>
  `,
})
export class AlertComponent {
  @Input() type: 'success' | 'error' | 'info' = 'info';
  @Input() message = '';
  @Output() dismissed = new EventEmitter<void>();
}
