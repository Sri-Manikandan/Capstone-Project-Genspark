import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-pagination',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="flex items-center justify-center gap-1.5 py-8" *ngIf="totalPages > 1" aria-label="Pagination">
      <button class="rounded-full border border-line bg-surface px-3.5 py-1.5 text-sm font-medium text-ink-soft transition hover:border-ink/30 disabled:opacity-40"
              [disabled]="currentPage <= 1" (click)="goTo(currentPage - 1)">Prev</button>
      <button *ngFor="let p of pages()"
              class="h-9 w-9 rounded-full border text-sm font-medium transition"
              [class.bg-plum]="p === currentPage"
              [class.text-white]="p === currentPage"
              [class.border-plum]="p === currentPage"
              [class.border-line]="p !== currentPage"
              [class.text-ink-soft]="p !== currentPage"
              (click)="goTo(p)">{{ p }}</button>
      <button class="rounded-full border border-line bg-surface px-3.5 py-1.5 text-sm font-medium text-ink-soft transition hover:border-ink/30 disabled:opacity-40"
              [disabled]="currentPage >= totalPages" (click)="goTo(currentPage + 1)">Next</button>
    </nav>
  `,
})
export class PaginationComponent {
  @Input() currentPage = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();

  protected pages(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goTo(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) return;
    this.pageChange.emit(page);
  }
}
