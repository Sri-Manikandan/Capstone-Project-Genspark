import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../../core/services/admin.service';
import { OrganizerRequestDto } from '../../../core/models/admin.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-organizer-requests',
  standalone: true,
  imports: [CommonModule, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Organizer Requests</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <table *ngIf="!loading()" class="w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-sm">
      <thead class="bg-gray-50 text-left text-gray-600">
        <tr><th class="p-3">User</th><th class="p-3">Requested</th><th class="p-3">Actions</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let r of requests()" class="border-t border-gray-100">
          <td class="p-3"><span class="font-medium text-gray-900">{{ r.userName }}</span><br /><span class="text-gray-500">{{ r.userEmail }}</span></td>
          <td class="p-3 text-gray-600">{{ r.requestedAt | istDate }}</td>
          <td class="p-3">
            <button (click)="approve(r.id)" class="mr-2 text-green-600 hover:underline">Approve</button>
            <button (click)="reject(r.id)" class="text-red-600 hover:underline">Reject</button>
          </td>
        </tr>
      </tbody>
    </table>
    <p *ngIf="!loading() && requests().length === 0" class="py-10 text-center text-gray-500">No pending requests.</p>
  `,
})
export class OrganizerRequestsComponent implements OnInit {
  private admin = inject(AdminService);

  protected requests = signal<OrganizerRequestDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');

  ngOnInit(): void { this.load(); }

  protected approve(id: number): void {
    this.admin.approveOrganizerRequest(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }
  protected reject(id: number): void {
    this.admin.rejectOrganizerRequest(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getOrganizerRequests({ status: 'Pending', page: 1, pageSize: 50 }).subscribe({
      next: res => { this.requests.set(res.items); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
