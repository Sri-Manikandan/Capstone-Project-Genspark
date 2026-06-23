import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../../core/services/admin.service';
import { OrganizerRequestDto } from '../../../core/models/admin.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-organizer-requests',
  standalone: true,
  imports: [CommonModule, LoadingSpinnerComponent, AlertComponent, PaginationComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Organizer requests</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="overflow-x-auto">
      <table class="data-table">
        <thead>
          <tr><th>User</th><th>Requested</th><th>Actions</th></tr>
        </thead>
        <tbody>
          <tr *ngFor="let r of requests()">
            <td><span class="font-medium text-ink">{{ r.userName }}</span><br /><span class="text-muted">{{ r.userEmail }}</span></td>
            <td class="font-mono text-xs">{{ r.requestedAt | istDate }}</td>
            <td>
              <button (click)="approve(r.id)" class="link-go mr-3">Approve</button>
              <button (click)="reject(r.id)" class="link-danger">Reject</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p *ngIf="!loading() && requests().length === 0" class="card mt-2 px-6 py-16 text-center text-muted">No pending requests.</p>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class OrganizerRequestsComponent implements OnInit {
  private admin = inject(AdminService);

  protected requests = signal<OrganizerRequestDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);

  ngOnInit(): void { this.load(); }

  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected approve(id: number): void {
    this.admin.approveOrganizerRequest(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }
  protected reject(id: number): void {
    this.admin.rejectOrganizerRequest(id, {}).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getOrganizerRequests({ status: 'Pending', page: this.page(), pageSize: 10 }).subscribe({
      next: res => { this.requests.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
