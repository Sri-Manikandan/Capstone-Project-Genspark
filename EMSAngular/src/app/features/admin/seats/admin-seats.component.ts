import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SeatService } from '../../../core/services/seat.service';
import { VenueService } from '../../../core/services/venue.service';
import { ToastService } from '../../../core/services/toast.service';
import { SeatDto } from '../../../core/models/seat.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { BuilderCell, generateGrid, gridToSeats, seatsToGrid } from './seat-grid';

const AISLE = 'Aisle';

// A screen is laid out by hand, so these bounds are generous for any real auditorium while
// stopping a typo (100 instead of 10) from building a grid big enough to lock up the page.
const MAX_ROWS = 100;
const MAX_PER_ROW = 100;
const MAX_SEAT_TYPE_LENGTH = 50;

@Component({
  selector: 'ems-admin-seats',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-seats.component.html',
})
export class AdminSeatsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private seatService = inject(SeatService);
  private venueService = inject(VenueService);
  private toast = inject(ToastService);

  // Seats are laid out per screen but capacity is declared for the whole venue, so the
  // builder needs the venue's limit to check a screen against the other screens' seats.
  protected venueCapacity = signal<number | null>(null);

  protected seats = signal<SeatDto[]>([]);
  protected loading = signal(false);
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

  // Seats in every screen except the one being edited; those are the seats a saved
  // screen has to fit alongside.
  protected seatsInOtherScreens = computed(() => {
    const screen = this.selectedScreen();
    return this.seats().filter(s => s.section !== screen).length;
  });

  ngOnInit(): void {
    this.venueService.getById(this.venueId).subscribe({
      next: v => this.venueCapacity.set(v.totalCapacity),
      error: (m: string) => this.toast.error(m),
    });
    this.load();
  }

  protected selectScreen(name: string): void {
    this.selectedScreen.set(name);
    const screenSeats = this.seats().filter(s => s.section === name);
    this.grid.set(seatsToGrid(screenSeats));
  }

  protected addScreen(): void {
    const name = this.newScreenName.trim();
    if (!name) {
      this.toast.error('Screen name is required.');
      return;
    }
    // Reusing an existing name would silently point the builder at that screen and
    // overwrite its seats on save, so treat it as a mistake rather than a selection.
    if (this.screens().includes(name)) {
      this.toast.error(`Screen "${name}" already exists.`);
      return;
    }
    this.newScreenName = '';
    this.selectedScreen.set(name);
    this.grid.set([]);
  }

  protected addType(): void {
    const type = this.newType.trim();
    if (!type) {
      this.toast.error('Seat type name is required.');
      return;
    }
    if (type.length > MAX_SEAT_TYPE_LENGTH) {
      this.toast.error(`Seat type must be ${MAX_SEAT_TYPE_LENGTH} characters or fewer.`);
      return;
    }
    // A ticket type's quantity is the count of venue seats whose type matches by name, so
    // "VIP" and "vip" would split one tier's seats across two types. Treat them as the same
    // name — this also stops a seat type shadowing the Aisle marker.
    const clash = this.palette().find(p => p.toLowerCase() === type.toLowerCase());
    if (clash) {
      this.toast.error(`Seat type "${clash}" already exists.`);
      this.newType = '';
      return;
    }
    this.palette.update(p => [...p.slice(0, -1), type, AISLE]); // keep Aisle last
    this.newType = '';
  }

  protected generate(): void {
    const rows = Number(this.rows);
    const perRow = Number(this.perRow);
    if (!this.isWithinBounds(rows, MAX_ROWS) || !this.isWithinBounds(perRow, MAX_PER_ROW)) {
      this.toast.error(`Rows and seats per row must be whole numbers between 1 and ${MAX_ROWS}.`);
      return;
    }
    // Refuse a grid that cannot legally be saved, rather than letting it be laid out and
    // only rejecting it at save time.
    if (!this.fitsInVenueCapacity(rows * perRow)) return;

    const base = this.paint() === AISLE ? 'Normal' : this.paint();
    this.grid.set(generateGrid(rows, perRow, base));
  }

  /**
   * Seats in this screen have to fit alongside every other screen's inside the venue's
   * declared capacity. Mirrors the API rule so the limit is explained here rather than
   * coming back as a 400. Reports the failure itself so callers just bail out.
   */
  private fitsInVenueCapacity(seatCount: number): boolean {
    const capacity = this.venueCapacity();
    if (capacity === null) return true; // still loading; the API remains the backstop

    const total = this.seatsInOtherScreens() + seatCount;
    if (total > capacity) {
      this.toast.error(`Venue capacity is ${capacity} seats. This would bring the venue to ${total} seats across all screens.`);
      return false;
    }
    return true;
  }

  private isWithinBounds(value: number, max: number): boolean {
    return Number.isInteger(value) && value >= 1 && value <= max;
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
    if (seats.length === 0) { this.toast.error('Add at least one seat before saving.'); return; }

    if (!this.fitsInVenueCapacity(seats.length)) return;

    this.seatService.setScreenSeats({ venueId: this.venueId, screen, seats }).subscribe({
      next: () => { this.toast.success('Screen saved.'); this.load(); },
      error: (m: string) => this.toast.error(m),
    });
  }

  protected deleteScreen(): void {
    const screen = this.selectedScreen();
    if (!screen) return;
    if (!confirm(`Delete screen "${screen}" and all its seats?`)) return;
    this.seatService.deleteScreen(this.venueId, screen).subscribe({
      next: () => { this.toast.success('Screen deleted.'); this.selectedScreen.set(null); this.grid.set([]); this.load(); },
      error: (m: string) => this.toast.error(m),
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
      error: (m: string) => { this.toast.error(m); this.loading.set(false); },
    });
  }
}
