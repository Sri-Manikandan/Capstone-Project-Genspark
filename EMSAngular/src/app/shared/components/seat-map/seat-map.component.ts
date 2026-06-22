import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeatService } from '../../../core/services/seat.service';
import { SeatHubService } from '../../../core/services/seat-hub.service';
import { SeatDto } from '../../../core/models/seat.model';

interface SeatRow { row: string; seats: SeatDto[]; }
interface SeatSection { section: string; rows: SeatRow[]; }

@Component({
  selector: 'ems-seat-map',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-x-auto">
      <div class="mb-5 rounded-xl bg-paper py-2 text-center font-mono text-[0.66rem] uppercase tracking-eyebrow text-muted">
        Stage / Screen
      </div>
      <div class="mb-5 flex flex-wrap gap-4 font-mono text-xs text-ink-soft">
        <span class="flex items-center gap-1.5"><i class="h-3.5 w-3.5 rounded border border-plum bg-surface"></i> Available</span>
        <span class="flex items-center gap-1.5"><i class="h-3.5 w-3.5 rounded bg-plum"></i> Selected</span>
        <span class="flex items-center gap-1.5"><i class="h-3.5 w-3.5 rounded bg-line"></i> Taken</span>
      </div>
      <div class="space-y-6">
        <div *ngFor="let section of sections()">
          <h4 class="eyebrow mb-2">Section {{ section.section }}</h4>
          <div class="space-y-1.5">
            <div *ngFor="let r of section.rows" class="flex items-center gap-1.5">
              <span class="w-6 font-mono text-xs text-muted">{{ r.row }}</span>
              <button *ngFor="let seat of r.seats" type="button"
                      class="h-8 w-8 rounded-md border text-xs font-medium transition"
                      [class.border-plum]="seatState(seat) === 'available'"
                      [class.text-plum]="seatState(seat) === 'available'"
                      [class.bg-surface]="seatState(seat) === 'available'"
                      [class.bg-plum]="seatState(seat) === 'selected'"
                      [class.border-plum]="seatState(seat) === 'selected'"
                      [class.text-white]="seatState(seat) === 'selected'"
                      [class.bg-line]="seatState(seat) === 'taken'"
                      [class.border-line]="seatState(seat) === 'taken'"
                      [class.text-muted]="seatState(seat) === 'taken'"
                      [class.cursor-not-allowed]="seatState(seat) === 'taken'"
                      [disabled]="seatState(seat) === 'taken'"
                      (click)="onSeatClick(seat)">{{ seat.seatNumber }}</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class SeatMapComponent implements OnInit, OnDestroy {
  private seatService = inject(SeatService);
  private hub = inject(SeatHubService);

  @Input({ required: true }) eventId!: number;
  @Input({ required: true }) venueId!: number;
  @Input() selectedSeatIds: number[] = [];
  @Output() seatToggled = new EventEmitter<SeatDto>();

  private allSeats = signal<SeatDto[]>([]);
  private availableIds = signal<Set<number>>(new Set());
  protected sections = computed<SeatSection[]>(() => this.group(this.allSeats()));

  constructor() {
    effect(() => {
      const update = this.hub.lastUpdate();
      if (!update) return;
      this.availableIds.update(set => {
        const next = new Set(set);
        if (update.status === 'released') next.add(update.seatId);
        else next.delete(update.seatId);
        return next;
      });
    });
  }

  ngOnInit(): void {
    this.seatService.getAvailableByEvent(this.eventId).subscribe(seats => {
      this.allSeats.set(seats);
      this.availableIds.set(new Set(seats.map(s => s.id)));
    });
    void this.hub.joinEvent(this.eventId);
  }

  ngOnDestroy(): void {
    void this.hub.leaveEvent(this.eventId);
  }

  protected seatState(seat: SeatDto): 'selected' | 'available' | 'taken' {
    if (this.selectedSeatIds.includes(seat.id)) return 'selected';
    return this.availableIds().has(seat.id) ? 'available' : 'taken';
  }

  protected onSeatClick(seat: SeatDto): void {
    if (this.seatState(seat) === 'taken') return;
    this.seatToggled.emit(seat);
  }

  private group(seats: SeatDto[]): SeatSection[] {
    const bySection = new Map<string, Map<string, SeatDto[]>>();
    for (const s of seats) {
      if (!bySection.has(s.section)) bySection.set(s.section, new Map());
      const rows = bySection.get(s.section)!;
      if (!rows.has(s.row)) rows.set(s.row, []);
      rows.get(s.row)!.push(s);
    }
    return [...bySection.entries()].map(([section, rows]) => ({
      section,
      rows: [...rows.entries()].map(([row, rowSeats]) => ({
        row,
        seats: rowSeats.sort((a, b) => a.seatNumber - b.seatNumber),
      })),
    }));
  }
}
