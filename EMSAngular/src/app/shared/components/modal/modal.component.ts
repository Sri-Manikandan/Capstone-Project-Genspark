import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-modal',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './modal.component.html',
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
