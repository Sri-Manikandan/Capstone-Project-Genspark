import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { PendingEventReview } from '../../../core/models/event.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-approvals',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-approvals.component.html',
})
export class EventApprovalsComponent implements OnInit {
  private admin = inject(AdminService);
  private toast = inject(ToastService);

  protected events = signal<PendingEventReview[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected reasons = signal<Record<number, string>>({});

  ngOnInit(): void { this.load(); }

  protected setReason(id: number, value: string): void {
    this.reasons.update(r => ({ ...r, [id]: value }));
  }

  protected approve(id: number): void {
    this.admin.approveEvent(id, {}).subscribe({
      next: () => { this.toast.success('Event approved.'); this.load(); },
      error: (m: string) => this.toast.error(m),
    });
  }

  protected reject(id: number): void {
    const reason = (this.reasons()[id] ?? '').trim();
    if (!reason) { this.toast.error('Enter a reason before rejecting.'); return; }
    this.admin.rejectEvent(id, { reason }).subscribe({
      next: () => { this.toast.success('Event rejected.'); this.load(); },
      error: (m: string) => this.toast.error(m),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getPendingEvents().subscribe({
      next: events => { this.events.set(events ?? []); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
