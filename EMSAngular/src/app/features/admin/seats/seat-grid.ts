import { SeatDto, ScreenSeat } from '../../../core/models/seat.model';

export interface BuilderCell {
  row: string;
  number: number;
  type: string;
  active: boolean;
}

export function rowLabel(index: number): string {
  let n = index + 1;
  let s = '';
  while (n > 0) {
    const rem = (n - 1) % 26;
    s = String.fromCharCode(65 + rem) + s;
    n = Math.floor((n - 1) / 26);
  }
  return s;
}

export function generateGrid(rows: number, perRow: number, defaultType: string): BuilderCell[][] {
  const grid: BuilderCell[][] = [];
  for (let r = 0; r < rows; r++) {
    const label = rowLabel(r);
    const cells: BuilderCell[] = [];
    for (let c = 0; c < perRow; c++) {
      cells.push({ row: label, number: c + 1, type: defaultType, active: true });
    }
    grid.push(cells);
  }
  return grid;
}

export function gridToSeats(grid: BuilderCell[][]): ScreenSeat[] {
  const seats: ScreenSeat[] = [];
  for (const row of grid) {
    let seatNo = 0;
    for (const cell of row) {
      if (!cell.active) continue;
      seatNo += 1;
      seats.push({ row: cell.row, seatNumber: seatNo, seatType: cell.type });
    }
  }
  return seats;
}

export function seatsToGrid(seats: SeatDto[]): BuilderCell[][] {
  const byRow = new Map<string, SeatDto[]>();
  for (const s of seats) {
    if (!byRow.has(s.row)) byRow.set(s.row, []);
    byRow.get(s.row)!.push(s);
  }
  return [...byRow.entries()]
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([row, rowSeats]) =>
      rowSeats
        .sort((a, b) => a.seatNumber - b.seatNumber)
        .map(s => ({ row, number: s.seatNumber, type: s.seatType, active: true })));
}
