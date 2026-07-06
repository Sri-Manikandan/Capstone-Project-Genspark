import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-pagination',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pagination.component.html',
})
export class PaginationComponent {
  @Input() currentPage = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();

  protected readonly ellipsis = '…';

  // Windowed page list: always show first/last plus a small window around the current
  // page, with ellipsis for the gaps, so the control never overflows on small screens.
  protected pages(): (number | string)[] {
    const total = this.totalPages;
    const current = this.currentPage;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

    const left = Math.max(2, current - 1);
    const right = Math.min(total - 1, current + 1);
    const result: (number | string)[] = [1];

    if (left > 2) result.push(this.ellipsis);
    for (let p = left; p <= right; p++) result.push(p);
    if (right < total - 1) result.push(this.ellipsis);
    result.push(total);
    return result;
  }

  goTo(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) return;
    this.pageChange.emit(page);
  }
}
