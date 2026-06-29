import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'ems-admin-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.css',
})
export class AdminLayoutComponent {
  protected readonly drawerOpen = signal(false);

  protected readonly links = [
    { path: 'events', label: 'Event approvals' },
    { path: 'organizer-requests', label: 'Organizer requests' },
    { path: 'users', label: 'Users' },
    { path: 'venues', label: 'Venues' },
  ];

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }
}
