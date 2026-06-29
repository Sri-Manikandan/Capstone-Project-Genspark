import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'ems-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './navbar.component.html',
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
