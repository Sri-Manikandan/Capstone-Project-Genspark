import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../../core/services/admin.service';
import { OrganizerRequestDto, OrganizerRequestQueryRequest } from '../../../core/models/admin.model';
import { OrganizerRequestFilterStore } from './organizer-request-filter.store';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-organizer-requests',
  standalone: true,
  imports: [CommonModule, LoadingSpinnerComponent, AlertComponent, PaginationComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './organizer-requests.component.html',
})
export class OrganizerRequestsComponent {
  private admin = inject(AdminService);
  protected store = inject(OrganizerRequestFilterStore);

  protected requests = signal<OrganizerRequestDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected totalPages = signal(1);

  constructor() {
    effect(() => {
      const req = this.store.request();
      this.load(req);
    });
  }

  protected setStatus(status: string): void { this.store.patch({ status }); }
  protected goToPage(p: number): void { this.store.setPage(p); }

  protected approve(id: number): void {
    this.admin.approveOrganizerRequest(id, {}).subscribe({ next: () => this.load(this.store.request()), error: (m: string) => this.error.set(m) });
  }
  protected reject(id: number): void {
    this.admin.rejectOrganizerRequest(id, {}).subscribe({ next: () => this.load(this.store.request()), error: (m: string) => this.error.set(m) });
  }

  private load(req: OrganizerRequestQueryRequest): void {
    this.loading.set(true);
    this.admin.getOrganizerRequests(req).subscribe({
      next: res => { this.requests.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
