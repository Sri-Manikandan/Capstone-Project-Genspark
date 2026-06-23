import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'ems-admin-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-8 lg:flex-row">
      <aside class="lg:w-56 lg:shrink-0">
        <p class="eyebrow mb-3 px-3">Admin</p>
        <nav class="flex gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
          @for (item of links; track item.path) {
            <a
              [routerLink]="item.path"
              routerLinkActive="bg-surface text-ink"
              class="side-link"
            >{{ item.label }}</a>
          }
        </nav>
      </aside>

      <section class="min-w-0 flex-1">
        <router-outlet />
      </section>
    </div>
  `,
  styles: `
    .side-link {
      @apply whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium text-ink-soft transition hover:bg-surface hover:text-ink;
    }
  `,
})
export class AdminLayoutComponent {
  protected readonly links = [
    { path: 'events', label: 'Event approvals' },
    { path: 'organizer-requests', label: 'Organizer requests' },
    { path: 'users', label: 'Users' },
    { path: 'venues', label: 'Venues' },
  ];
}
