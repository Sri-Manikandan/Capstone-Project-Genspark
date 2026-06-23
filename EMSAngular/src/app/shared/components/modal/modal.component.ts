import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-modal',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *ngIf="open()" class="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div class="absolute inset-0 bg-ink/40 backdrop-blur-sm" (click)="close()"></div>
      <div class="relative z-10 w-full max-w-xl rounded-2xl border border-line bg-surface p-6 shadow-xl">
        <div class="mb-4 flex items-center justify-between">
          <h2 class="eyebrow">{{ title() }}</h2>
          <button type="button" class="text-muted transition hover:text-ink" aria-label="Close dialog" (click)="close()">×</button>
        </div>
        <ng-content></ng-content>
      </div>
    </div>
  `,
})
export class ModalComponent {
  readonly title = input('');
  readonly open = input(false);
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  protected close(): void {
    if (this.open()) this.closed.emit();
  }
}
