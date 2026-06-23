import { rowLabel, generateGrid, gridToSeats, seatsToGrid } from './seat-grid';

describe('seat-grid helpers', () => {
  it('rowLabel produces A, Z, AA', () => {
    expect(rowLabel(0)).toBe('A');
    expect(rowLabel(25)).toBe('Z');
    expect(rowLabel(26)).toBe('AA');
  });

  it('generateGrid builds rows x perRow active cells', () => {
    const grid = generateGrid(2, 3, 'Normal');
    expect(grid.length).toBe(2);
    expect(grid[0].length).toBe(3);
    expect(grid[0][0]).toEqual({ row: 'A', number: 1, type: 'Normal', active: true });
    expect(grid[1][2].row).toBe('B');
  });

  it('gridToSeats skips inactive cells and renumbers per row', () => {
    const grid = generateGrid(1, 3, 'Normal');
    grid[0][1].active = false; // carve an aisle in the middle
    const seats = gridToSeats(grid);
    expect(seats).toEqual([
      { row: 'A', seatNumber: 1, seatType: 'Normal' },
      { row: 'A', seatNumber: 2, seatType: 'Normal' },
    ]);
  });

  it('seatsToGrid rebuilds a grid from saved seats', () => {
    const grid = seatsToGrid([
      { id: 1, venueId: 1, section: 'S1', row: 'A', seatNumber: 1, seatType: 'Premium' },
      { id: 2, venueId: 1, section: 'S1', row: 'A', seatNumber: 2, seatType: 'Premium' },
    ]);
    expect(grid.length).toBe(1);
    expect(grid[0].map(c => c.type)).toEqual(['Premium', 'Premium']);
  });
});
