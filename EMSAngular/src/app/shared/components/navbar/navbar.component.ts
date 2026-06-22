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
    <header class="sticky top-0 z-30 border-b border-line bg-paper/85 backdrop-blur">
      <nav class="relative mx-auto flex max-w-6xl items-center justify-between px-4 py-3.5">
        <a routerLink="/events" (click)="menuOpen.set(false)" class="group flex items-center gap-2" aria-label="EventHub home">
          <span class="grid h-7 w-7 place-items-center rounded-md bg-plum font-mono text-xs font-bold text-white">E</span>
          <span class="font-display text-xl font-semibold tracking-tight text-ink">EventHub</span>
        </a>

        <button class="rounded-lg p-2 text-2xl leading-none text-ink sm:hidden"
                (click)="menuOpen.set(!menuOpen())" [attr.aria-expanded]="menuOpen()" aria-label="Toggle menu">
          {{ menuOpen() ? '✕' : '☰' }}
        </button>

        <div
          class="absolute inset-x-0 top-full flex flex-col gap-1 border-b border-line bg-paper p-4 shadow-card
                 sm:static sm:flex sm:flex-row sm:items-center sm:gap-1 sm:border-0 sm:bg-transparent sm:p-0 sm:shadow-none"
          [class.hidden]="!menuOpen()">
          <a routerLink="/events" routerLinkActive="text-plum" (click)="menuOpen.set(false)" class="nav-link">Events</a>

          <ng-container *ngIf="auth.isAuthenticated(); else guestLinks">
            <a routerLink="/bookings" routerLinkActive="text-plum" (click)="menuOpen.set(false)" class="nav-link">My Bookings</a>
            <a *ngIf="canOrganize()" routerLink="/organizer/events" routerLinkActive="text-plum" (click)="menuOpen.set(false)" class="nav-link">My Events</a>
            <a *ngIf="auth.role() === 'Admin'" routerLink="/admin/events" routerLinkActive="text-plum" (click)="menuOpen.set(false)" class="nav-link">Admin</a>
            <span class="px-3 py-1 text-sm text-muted sm:ml-2 sm:px-0 sm:py-0">{{ auth.currentUser()?.name }}</span>
            <button (click)="logout()" class="btn-ghost btn-sm mt-1 sm:ml-1 sm:mt-0">Logout</button>
          </ng-container>

          <ng-template #guestLinks>
            <a routerLink="/auth/login" (click)="menuOpen.set(false)" class="nav-link">Login</a>
            <a routerLink="/auth/register" (click)="menuOpen.set(false)" class="btn-primary btn-sm mt-1 sm:ml-1 sm:mt-0">Register</a>
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
