import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './shared/components/navbar/navbar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent],
  template: `
    <ems-navbar />
    <main class="mx-auto max-w-6xl px-4 py-6">
      <router-outlet />
    </main>
  `,
})
export class App {}
