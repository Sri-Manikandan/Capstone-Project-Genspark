import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { EventDto } from '../../../core/models/event.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-approvals',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, AlertComponent, PaginationComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Pending events</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="space-y-3">
      <div *ngFor="let ev of events()" class="card p-5">
        <p class="font-display text-lg font-semibold text-ink">{{ ev.title }}</p>
        <p class="font-mono text-xs text-muted">{{ ev.startTime | istDate }} · {{ ev.category }}</p>
        <p class="mt-2 text-sm text-ink-soft">{{ ev.description }}</p>
        <div class="mt-4 flex flex-wrap items-center gap-2">
          <button (click)="approve(ev.id)" class="btn-primary btn-sm">Approve</button>
          <input [ngModel]="reasons()[ev.id] ?? ''" (ngModelChange)="setReason(ev.id, $event)"
                 aria-label="Rejection reason" placeholder="Rejection reason" class="field flex-1 py-1.5" />
          <button (click)="reject(ev.id)" class="btn-danger btn-sm">Reject</button>
        </div>
      </div>
      <p *ngIf="events().length === 0" class="card px-6 py-16 text-center text-muted">No pending events.</p>
    </div>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class EventApprovalsComponent implements OnInit {
  private admin = inject(AdminService);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected reasons = signal<Record<number, string>>({});
  protected page = signal(1);
  protected totalPages = signal(1);

  ngOnInit(): void { this.load(); }

  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected setReason(id: number, value: string): void {
    this.reasons.update(r => ({ ...r, [id]: value }));
  }

  protected approve(id: number): void {
    this.admin.approveEvent(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  protected reject(id: number): void {
    const reason = this.reasons()[id] || undefined;
    this.admin.rejectEvent(id, { reason }).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getPendingEvents(this.page(), 10).subscribe({
      next: res => { this.events.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
