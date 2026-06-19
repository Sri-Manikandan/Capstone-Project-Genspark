import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'ems-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="border-b border-gray-200 bg-white">
      <nav class="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <a routerLink="/events" class="text-lg font-bold text-indigo-600">EventHub</a>

        <button class="sm:hidden" (click)="menuOpen.set(!menuOpen())" aria-label="Toggle menu">&#9776;</button>

        <div class="hidden items-center gap-4 sm:flex" [class.flex]="menuOpen()" [class.hidden]="!menuOpen()">
          <a routerLink="/events" routerLinkActive="text-indigo-600" class="text-sm text-gray-700">Events</a>

          <ng-container *ngIf="auth.isAuthenticated(); else guestLinks">
            <a routerLink="/bookings" class="text-sm text-gray-700">My Bookings</a>
            <a *ngIf="canOrganize()" routerLink="/organizer/events" class="text-sm text-gray-700">My Events</a>
            <a *ngIf="auth.role() === 'Admin'" routerLink="/admin/events" class="text-sm text-gray-700">Admin</a>
            <span class="text-sm text-gray-500">{{ auth.currentUser()?.name }}</span>
            <button (click)="logout()" class="rounded-lg border border-gray-300 px-3 py-1 text-sm text-gray-700 hover:bg-gray-50">Logout</button>
          </ng-container>

          <ng-template #guestLinks>
            <a routerLink="/auth/login" class="text-sm text-gray-700">Login</a>
            <a routerLink="/auth/register" class="rounded-lg bg-indigo-600 px-3 py-1 text-sm text-white hover:bg-indigo-700">Register</a>
          </ng-template>
        </div>
      </nav>
    </header>
  `,
})
export class NavbarComponent {
  protected auth = inject(AuthService);
  private router = inject(Router);

  protected menuOpen = signal(false);
  protected canOrganize = computed(() => this.auth.role() === 'Organizer' || this.auth.role() === 'Admin');

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/events']);
  }
}
