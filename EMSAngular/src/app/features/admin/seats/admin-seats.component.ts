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
  templateUrl: './admin-seats.component.html',
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
