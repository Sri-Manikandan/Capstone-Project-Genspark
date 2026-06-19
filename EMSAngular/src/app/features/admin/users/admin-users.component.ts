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
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Users</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <form [formGroup]="filters" (ngSubmit)="applyFilters()" class="mb-4 flex gap-3">
      <input formControlName="query" placeholder="Search name or email…" class="flex-1 rounded-lg border border-gray-300 px-3 py-2" />
      <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Search</button>
    </form>
    <ems-loading-spinner *ngIf="loading()" />

    <table *ngIf="!loading()" class="w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-sm">
      <thead class="bg-gray-50 text-left text-gray-600">
        <tr><th class="p-3">Name</th><th class="p-3">Email</th><th class="p-3">Role</th><th class="p-3">Active</th><th class="p-3"></th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let u of users()" class="border-t border-gray-100">
          <td class="p-3 font-medium text-gray-900">{{ u.name }}</td>
          <td class="p-3 text-gray-600">{{ u.email }}</td>
          <td class="p-3 text-gray-600">{{ u.role }}</td>
          <td class="p-3 text-gray-600">{{ u.isActive ? 'Yes' : 'No' }}</td>
          <td class="p-3"><button (click)="remove(u.id)" class="text-red-600 hover:underline">Delete</button></td>
        </tr>
      </tbody>
    </table>
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
    this.admin.getUsers({ query: this.filters.getRawValue().query, page: this.page(), pageSize: 20 }).subscribe({
      next: res => { this.users.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
