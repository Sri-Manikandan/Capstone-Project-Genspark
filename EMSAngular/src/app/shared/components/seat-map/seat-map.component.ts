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
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { CurrencyInrPipe } from '../../pipes/currency-inr.pipe';

interface SeatRow { row: string; seats: SeatDto[]; }
interface SeatSection { section: string; rows: SeatRow[]; }

@Component({
  selector: 'ems-seat-map',
  standalone: true,
  imports: [CommonModule, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './seat-map.component.html',
})
export class SeatMapComponent implements OnInit, OnDestroy {
  private seatService = inject(SeatService);
  private hub = inject(SeatHubService);

  @Input({ required: true }) eventId!: number;
  @Input({ required: true }) venueId!: number;
  @Input() screenName = '';
  @Input() selectedSeatIds: number[] = [];
  @Input() ticketTypes: TicketTypeDto[] = [];
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

  protected sectionMeta(section: SeatSection): TicketTypeDto | undefined {
    const seatType = section.rows[0]?.seats[0]?.seatType;
    if (!seatType) return undefined;
    return this.ticketTypes.find(t => t.seatType.toLowerCase() === seatType.toLowerCase());
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
