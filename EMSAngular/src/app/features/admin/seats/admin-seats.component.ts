import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SeatService } from '../../../core/services/seat.service';
import { SeatDto } from '../../../core/models/seat.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { BuilderCell, generateGrid, gridToSeats, seatsToGrid } from './seat-grid';

const AISLE = 'Aisle';

@Component({
  selector: 'ems-admin-seats',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Admin</p>
    <h1 class="page-title mt-2 mb-6">Screens & seats</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-alert type="success" [message]="success()" (dismissed)="success.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="!loading()" class="grid grid-cols-1 gap-6 lg:grid-cols-[14rem_1fr]">
      <!-- Screen list -->
      <aside class="space-y-2">
        <h2 class="eyebrow mb-2">Screens</h2>
        <button *ngFor="let s of screens()" type="button"
                class="block w-full rounded-lg border px-3 py-2 text-left text-sm transition"
                [class.border-plum]="s === selectedScreen()"
                [class.bg-paper]="s === selectedScreen()"
                [class.border-line]="s !== selectedScreen()"
                (click)="selectScreen(s)">{{ s }}</button>
        <div class="flex gap-2 pt-2">
          <input [(ngModel)]="newScreenName" aria-label="New screen name" placeholder="New screen" class="field flex-1" />
          <button type="button" (click)="addScreen()" class="btn-ghost">Add</button>
        </div>
      </aside>

      <!-- Builder -->
      <section *ngIf="selectedScreen() as screen" class="space-y-4">
        <div class="card flex flex-wrap items-end gap-3 p-4">
          <label class="block space-y-1">
            <span class="field-label">Rows</span>
            <input type="number" [(ngModel)]="rows" min="1" class="field w-24" />
          </label>
          <label class="block space-y-1">
            <span class="field-label">Seats / row</span>
            <input type="number" [(ngModel)]="perRow" min="1" class="field w-24" />
          </label>
          <button type="button" (click)="generate()" class="btn-ghost">Generate grid</button>
        </div>

        <div class="card p-4">
          <div class="mb-3 flex flex-wrap items-center gap-2">
            <span class="field-label">Paint:</span>
            <button *ngFor="let t of palette()" type="button"
                    class="rounded-lg border px-2.5 py-1 text-xs transition"
                    [class.border-plum]="t === paint()"
                    [class.bg-plum]="t === paint()"
                    [class.text-white]="t === paint()"
                    [class.border-line]="t !== paint()"
                    (click)="paint.set(t)">{{ t }}</button>
            <input [(ngModel)]="newType" aria-label="New seat type name" placeholder="+ type" class="field w-28" />
            <button type="button" (click)="addType()" class="btn-ghost">Add type</button>
          </div>

          <div class="mb-4 rounded-xl bg-paper py-2 text-center font-mono text-[0.66rem] uppercase tracking-eyebrow text-muted">
            Screen — {{ screen }}
          </div>

          <div class="space-y-1.5 overflow-x-auto select-none" (mouseleave)="painting = false">
            <div *ngFor="let row of grid(); let r = index" class="flex items-center gap-1.5">
              <span class="w-6 font-mono text-xs text-muted">{{ row[0]?.row }}</span>
              <button *ngFor="let cell of row; let c = index" type="button"
                      class="h-8 w-8 rounded-md border text-xs font-medium transition"
                      [class.border-dashed]="!cell.active"
                      [class.border-line]="!cell.active"
                      [class.text-muted]="!cell.active"
                      [class.border-plum]="cell.active && cell.type !== 'Premium'"
                      [class.text-plum]="cell.active && cell.type !== 'Premium'"
                      [class.bg-surface]="cell.active && cell.type !== 'Premium'"
                      [class.bg-plum]="cell.active && cell.type === 'Premium'"
                      [class.text-white]="cell.active && cell.type === 'Premium'"
                      (mousedown)="startPaint(r, c)"
                      (mouseenter)="dragPaint(r, c)">{{ cell.active ? '' : '·' }}</button>
            </div>
          </div>

          <div class="mt-4 flex gap-3">
            <button type="button" (click)="save()" class="btn-primary">Save screen</button>
            <span class="self-center text-sm text-muted">{{ seatCount() }} seats</span>
            <button type="button" (click)="deleteScreen()" class="link-danger self-center">Delete screen</button>
          </div>
        </div>
      </section>
    </div>
  `,
})
export class AdminSeatsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private seatService = inject(SeatService);

  protected seats = signal<SeatDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected success = signal('');
  protected selectedScreen = signal<string | null>(null);
  protected grid = signal<BuilderCell[][]>([]);
  protected paint = signal('Normal');
  protected palette = signal<string[]>(['Normal', 'Premium', AISLE]);

  protected rows = 6;
  protected perRow = 10;
  protected newScreenName = '';
  protected newType = '';
  protected painting = false;

  private venueId = Number(this.route.snapshot.paramMap.get('id'));

  protected screens = computed(() => [...new Set(this.seats().map(s => s.section))].sort());
  protected seatCount = computed(() => gridToSeats(this.grid()).length);

  ngOnInit(): void { this.load(); }

  protected selectScreen(name: string): void {
    this.selectedScreen.set(name);
    const screenSeats = this.seats().filter(s => s.section === name);
    this.grid.set(seatsToGrid(screenSeats));
  }

  protected addScreen(): void {
    const name = this.newScreenName.trim();
    if (!name) return;
    this.newScreenName = '';
    this.selectedScreen.set(name);
    this.grid.set([]);
  }

  protected addType(): void {
    const t = this.newType.trim();
    if (!t || this.palette().includes(t)) { this.newType = ''; return; }
    this.palette.update(p => [...p.slice(0, -1), t, AISLE]); // keep Aisle last
    this.newType = '';
  }

  protected generate(): void {
    const base = this.paint() === AISLE ? 'Normal' : this.paint();
    this.grid.set(generateGrid(Math.max(1, this.rows), Math.max(1, this.perRow), base));
  }

  protected startPaint(r: number, c: number): void {
    this.painting = true;
    this.applyPaint(r, c);
  }

  protected dragPaint(r: number, c: number): void {
    if (this.painting) this.applyPaint(r, c);
  }

  private applyPaint(r: number, c: number): void {
    this.grid.update(g => {
      const next = g.map(row => row.map(cell => ({ ...cell })));
      const cell = next[r]?.[c];
      if (!cell) return g;
      if (this.paint() === AISLE) { cell.active = false; }
      else { cell.active = true; cell.type = this.paint(); }
      return next;
    });
  }

  protected save(): void {
    const screen = this.selectedScreen();
    if (!screen) return;
    const seats = gridToSeats(this.grid());
    if (seats.length === 0) { this.error.set('Add at least one seat before saving.'); return; }
    this.seatService.setScreenSeats({ venueId: this.venueId, screen, seats }).subscribe({
      next: () => { this.success.set('Screen saved.'); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected deleteScreen(): void {
    const screen = this.selectedScreen();
    if (!screen) return;
    if (!confirm(`Delete screen "${screen}" and all its seats?`)) return;
    this.seatService.deleteScreen(this.venueId, screen).subscribe({
      next: () => { this.success.set('Screen deleted.'); this.selectedScreen.set(null); this.grid.set([]); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.seatService.getByVenue(this.venueId).subscribe({
      next: s => {
        this.seats.set(s);
        this.loading.set(false);
        const current = this.selectedScreen();
        if (current && this.screens().includes(current)) this.selectScreen(current);
      },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}
