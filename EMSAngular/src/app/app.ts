import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './shared/components/navbar/navbar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent],
  template: `
    <ems-navbar />
    <main class="mx-auto min-h-[70vh] max-w-6xl px-4 py-8 sm:py-12">
      <router-outlet />
    </main>
    <footer class="mt-16 border-t border-line">
      <div class="mx-auto flex max-w-6xl flex-col items-center justify-between gap-2 px-4 py-8 text-center sm:flex-row sm:text-left">
        <p class="font-display text-lg font-semibold text-ink">EventHub</p>
        <p class="eyebrow">Doors open · Lights down · Live</p>
      </div>
    </footer>
  `,
})
export class App {}
