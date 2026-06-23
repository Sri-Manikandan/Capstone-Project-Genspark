import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { User } from '../../../core/models/user.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PaginationComponent, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Users</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-5 flex gap-3">
      <input formControlName="query" placeholder="Search name or email…" class="field flex-1" />
      <button type="submit" class="btn-primary">Search</button>
    </form>
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="overflow-x-auto">
      <table class="data-table">
        <thead>
          <tr><th>Name</th><th>Email</th><th>Role</th><th>Active</th><th></th></tr>
        </thead>
        <tbody>
          <tr *ngFor="let u of users()">
            <td class="font-medium text-ink">{{ u.name }}</td>
            <td>{{ u.email }}</td>
            <td><span class="badge bg-paper text-ink-soft">{{ u.role }}</span></td>
            <td>{{ u.isActive ? 'Yes' : 'No' }}</td>
            <td><button (click)="remove(u.id)" class="link-danger">Delete</button></td>
          </tr>
        </tbody>
      </table>
    </div>
    <ems-pagination [currentPage]="page()" [totalPages]="totalPages()" (pageChange)="goToPage($event)" />
  `,
})
export class AdminUsersComponent implements OnInit {
  private admin = inject(AdminService);
  private fb = inject(FormBuilder);

  protected users = signal<User[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);
  protected filters = this.fb.nonNullable.group({ query: '' });

  ngOnInit(): void { this.load(); }
  protected applyFilters(): void { this.page.set(1); this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected remove(id: number): void {
    this.admin.deleteUser(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.admin.getUsers({ query: this.filters.getRawValue().query, page: this.page(), pageSize: 10 }).subscribe({
      next: res => { this.users.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
