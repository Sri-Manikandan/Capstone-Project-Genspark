import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EventService } from '../../core/services/event.service';
import { AuthService } from '../../core/services/auth.service';
import { EventDto } from '../../core/models/event.model';
import { EventCardComponent } from '../../shared/components/event-card/event-card.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'ems-home',
  standalone: true,
  imports: [CommonModule, RouterLink, EventCardComponent, LoadingSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- ── Hero ──────────────────────────────────────────────────── -->
    <section class="grid items-center gap-10 lg:grid-cols-2 lg:gap-14">
      <div>
        <p class="eyebrow text-plum">Live events · booked in seconds</p>
        <h1 class="mt-4 font-display text-4xl font-semibold leading-[1.05] tracking-tight text-ink text-balance sm:text-5xl lg:text-6xl">
          Your night out, <span class="italic text-plum">sorted</span>.
        </h1>
        <p class="mt-5 max-w-md text-lg leading-relaxed text-ink-soft">
          Concerts, conferences, comedy and more. Find the show, choose your exact seat,
          and walk in with a ticket on your phone.
        </p>
        <div class="mt-8 flex flex-wrap gap-3">
          <a routerLink="/events" class="btn-primary">Browse events</a>
          <a *ngIf="!auth.isAuthenticated()" routerLink="/auth/register" class="btn-ghost">Create account</a>
        </div>
      </div>

      <!-- Signature: a life-size admission ticket -->
      <div class="mx-auto w-full max-w-sm">
        <div class="overflow-hidden rounded-3xl border border-line bg-surface shadow-lift">
          <div class="bg-plum p-7 text-white">
            <span class="eyebrow text-white/70">Tonight's headliner</span>
            <p class="mt-2 font-display text-2xl font-semibold leading-tight">Midnight Symphony</p>
            <p class="mt-1 font-mono text-sm text-white/80">Sat · 9:00 PM · Grand Arena</p>
          </div>
          <div class="perf"></div>
          <div class="flex items-center justify-between gap-4 p-6">
            <div>
              <span class="eyebrow">Seat</span>
              <p class="font-mono text-xl text-ink">G-12</p>
            </div>
            <div class="h-10 flex-1 rounded"
                 style="background:repeating-linear-gradient(90deg,#241B2E 0 2px,transparent 2px 5px)"
                 aria-hidden="true"></div>
            <span class="eyebrow shrink-0">Admit one</span>
          </div>
        </div>
      </div>
    </section>

    <!-- ── On sale now ──────────────────────────────────────────── -->
    <section class="mt-20">
      <div class="mb-6 flex items-baseline justify-between gap-4">
        <div>
          <p class="eyebrow text-plum">On sale now</p>
          <h2 class="section-title mt-1">Upcoming events</h2>
        </div>
        <a routerLink="/events" class="link-action shrink-0">See all →</a>
      </div>

      <ems-loading-spinner *ngIf="loading()" />
      <div *ngIf="!loading()" class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <ems-event-card *ngFor="let ev of featured()" [event]="ev" />
      </div>
      <p *ngIf="!loading() && featured().length === 0" class="card px-6 py-12 text-center text-muted">
        No events published yet — check back soon.
      </p>
    </section>

    <!-- ── Browse by category ───────────────────────────────────── -->
    <section class="mt-20">
      <p class="eyebrow text-plum">Find your scene</p>
      <h2 class="section-title mt-1 mb-6">Browse by category</h2>
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        <a routerLink="/events" [queryParams]="{ category: 'Music' }"
           class="group rounded-2xl border border-line bg-surface p-5 transition hover:-translate-y-1 hover:shadow-card">
          <span class="badge bg-plum-tint text-plum-dark">Music</span>
          <p class="mt-3 font-display text-lg text-ink">Concerts & gigs</p>
        </a>
        <a routerLink="/events" [queryParams]="{ category: 'Technology' }"
           class="group rounded-2xl border border-line bg-surface p-5 transition hover:-translate-y-1 hover:shadow-card">
          <span class="badge bg-teal-tint text-teal-dark">Technology</span>
          <p class="mt-3 font-display text-lg text-ink">Talks & summits</p>
        </a>
        <a routerLink="/events" [queryParams]="{ category: 'Comedy' }"
           class="group rounded-2xl border border-line bg-surface p-5 transition hover:-translate-y-1 hover:shadow-card">
          <span class="badge bg-gold-tint text-gold">Comedy</span>
          <p class="mt-3 font-display text-lg text-ink">Stand-up nights</p>
        </a>
        <a routerLink="/events" [queryParams]="{ category: 'Art' }"
           class="group rounded-2xl border border-line bg-surface p-5 transition hover:-translate-y-1 hover:shadow-card">
          <span class="badge bg-rose-tint text-rose-dark">Art</span>
          <p class="mt-3 font-display text-lg text-ink">Shows & exhibits</p>
        </a>
        <a routerLink="/events" [queryParams]="{ category: 'Business' }"
           class="group rounded-2xl border border-line bg-surface p-5 transition hover:-translate-y-1 hover:shadow-card">
          <span class="badge bg-paper text-ink-soft">Business</span>
          <p class="mt-3 font-display text-lg text-ink">Pitches & meetups</p>
        </a>
      </div>
    </section>

    <!-- ── How it works ─────────────────────────────────────────── -->
    <section class="mt-20">
      <p class="eyebrow text-plum">From browse to backstage</p>
      <h2 class="section-title mt-1 mb-6">How it works</h2>
      <div class="grid grid-cols-1 gap-6 sm:grid-cols-3">
        <div class="card p-6">
          <span class="font-mono text-sm text-plum">01</span>
          <h3 class="mt-3 font-display text-xl text-ink">Find your event</h3>
          <p class="mt-2 text-sm leading-relaxed text-ink-soft">Search concerts, talks and shows, or browse by category and venue.</p>
        </div>
        <div class="card p-6">
          <span class="font-mono text-sm text-plum">02</span>
          <h3 class="mt-3 font-display text-xl text-ink">Pick your seat</h3>
          <p class="mt-2 text-sm leading-relaxed text-ink-soft">Choose exact seats on a live map — mix Silver, Gold and Premium in one booking.</p>
        </div>
        <div class="card p-6">
          <span class="font-mono text-sm text-plum">03</span>
          <h3 class="mt-3 font-display text-xl text-ink">Show your phone</h3>
          <p class="mt-2 text-sm leading-relaxed text-ink-soft">Pay securely and get a QR ticket instantly. Scan it at the door and you're in.</p>
        </div>
      </div>
    </section>

    <!-- ── Closing CTA ──────────────────────────────────────────── -->
    <section class="mt-20 overflow-hidden rounded-3xl bg-ink px-6 py-14 text-center sm:px-12">
      <p class="eyebrow text-white/60">Doors open soon</p>
      <h2 class="mx-auto mt-3 max-w-xl font-display text-3xl font-semibold text-white sm:text-4xl">
        Ready for your next night out?
      </h2>
      <div class="mt-7 flex flex-wrap justify-center gap-3">
        <a routerLink="/events" class="btn-primary">Browse events</a>
        <a *ngIf="!auth.isAuthenticated()" routerLink="/auth/register" class="btn bg-white text-ink hover:bg-paper">
          Create free account
        </a>
      </div>
    </section>
  `,
})
export class HomeComponent implements OnInit {
  private eventService = inject(EventService);
  protected auth = inject(AuthService);

  protected featured = signal<EventDto[]>([]);
  protected loading = signal(false);

  ngOnInit(): void {
    this.loading.set(true);
    this.eventService.search({ page: 1, pageSize: 3, sortBy: 'startTime', sortOrder: 'asc' }).subscribe({
      next: res => { this.featured.set(res.items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
