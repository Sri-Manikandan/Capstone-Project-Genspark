import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { EventDto } from '../../../core/models/event.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-event-approvals',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Pending Events</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="space-y-3">
      <div *ngFor="let ev of events()" class="rounded-lg border border-gray-200 bg-white p-4">
        <p class="font-medium text-gray-900">{{ ev.title }}</p>
        <p class="text-sm text-gray-500">{{ ev.startTime | istDate }} · {{ ev.category }}</p>
        <p class="mt-1 text-sm text-gray-700">{{ ev.description }}</p>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <button (click)="approve(ev.id)" class="rounded-lg bg-green-600 px-3 py-1 text-sm text-white hover:bg-green-700">Approve</button>
          <input [ngModel]="reasons()[ev.id] ?? ''" (ngModelChange)="setReason(ev.id, $event)"
                 placeholder="Rejection reason" class="flex-1 rounded-lg border border-gray-300 px-3 py-1 text-sm" />
          <button (click)="reject(ev.id)" class="rounded-lg bg-red-600 px-3 py-1 text-sm text-white hover:bg-red-700">Reject</button>
        </div>
      </div>
      <p *ngIf="events().length === 0" class="py-10 text-center text-gray-500">No pending events.</p>
    </div>
  `,
})
export class EventApprovalsComponent implements OnInit {
  private admin = inject(AdminService);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected reasons = signal<Record<number, string>>({});

  ngOnInit(): void { this.load(); }

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
    this.admin.getPendingEvents(1, 50).subscribe({
      next: res => { this.events.set(res.items); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
