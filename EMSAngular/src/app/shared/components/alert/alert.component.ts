import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-alert',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="message" class="flex items-start justify-between gap-3 rounded-lg px-4 py-3 text-sm"
         [class.bg-green-50]="type === 'success'" [class.text-green-700]="type === 'success'"
         [class.bg-red-50]="type === 'error'" [class.text-red-700]="type === 'error'"
         [class.bg-indigo-50]="type === 'info'" [class.text-indigo-700]="type === 'info'">
      <span>{{ message }}</span>
      <button class="font-bold opacity-60 hover:opacity-100" (click)="dismissed.emit()">×</button>
    </div>
  `,
})
export class AlertComponent {
  @Input() type: 'success' | 'error' | 'info' = 'info';
  @Input() message = '';
  @Output() dismissed = new EventEmitter<void>();
}
