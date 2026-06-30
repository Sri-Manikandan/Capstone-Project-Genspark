import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminService } from '../../../core/services/admin.service';
import { Role, User, UserSearchRequest } from '../../../core/models/user.model';
import { UserFilterStore, UserFilters } from './user-filter.store';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-admin-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PaginationComponent, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-users.component.html',
})
export class AdminUsersComponent {
  private admin = inject(AdminService);
  private fb = inject(FormBuilder);
  protected store = inject(UserFilterStore);

  protected users = signal<User[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected totalPages = signal(1);
  protected search = this.fb.nonNullable.control(this.store.filters().query);

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(query => this.store.patch({ query }));

    effect(() => {
      const req = this.store.request();
      this.load(req);
    });
  }

  protected setRole(role: string): void { this.store.patch({ role: role as Role | '' }); }
  protected setActive(active: string): void { this.store.patch({ active: active as UserFilters['active'] }); }
  protected goToPage(p: number): void { this.store.setPage(p); }

  protected clearFilters(): void {
    this.store.reset();
    this.search.setValue('', { emitEvent: false });
  }

  protected remove(id: number): void {
    this.admin.deleteUser(id).subscribe({ next: () => this.load(this.store.request()), error: (m: string) => this.error.set(m) });
  }

  private load(req: UserSearchRequest): void {
    this.loading.set(true);
    this.admin.getUsers(req).subscribe({
      next: res => { this.users.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
