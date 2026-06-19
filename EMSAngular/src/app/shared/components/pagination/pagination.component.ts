import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ems-pagination',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="flex items-center justify-center gap-1 py-4" *ngIf="totalPages > 1">
      <button class="px-3 py-1 rounded-lg border border-gray-300 text-gray-700 disabled:opacity-40"
              [disabled]="currentPage <= 1" (click)="goTo(currentPage - 1)">Prev</button>
      <button *ngFor="let p of pages()"
              class="px-3 py-1 rounded-lg border"
              [class.bg-indigo-600]="p === currentPage"
              [class.text-white]="p === currentPage"
              [class.border-indigo-600]="p === currentPage"
              [class.border-gray-300]="p !== currentPage"
              (click)="goTo(p)">{{ p }}</button>
      <button class="px-3 py-1 rounded-lg border border-gray-300 text-gray-700 disabled:opacity-40"
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
