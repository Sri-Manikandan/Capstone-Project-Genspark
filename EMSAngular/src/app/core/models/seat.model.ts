export interface SeatDto {
  id: number;
  venueId: number;
  section: string;
  row: string;
  seatNumber: number;
  seatType: string;
}

export interface CreateSeatRequest {
  venueId: number;
  section: string;
  row: string;
  seatNumber: number;
  seatType: string;
}

export interface BulkCreateSeatsRequest {
  venueId: number;
  section: string;
  row: string;
  startNumber: number;
  endNumber: number;
  seatType: string;
}

export interface ReserveSeatRequest {
  eventId: number;
  seatId: number;
  ticketTypeId: number;
}

export interface SeatReservationDto {
  id: number;
  seatId: number;
  eventId: number;
  ticketTypeId: number;
  userId: number;
  status: string;
  reservedUntil: string;
  createdAt: string;
}

export interface ScreenSeat {
  row: string;
  seatNumber: number;
  seatType: string;
}

export interface SetScreenSeatsRequest {
  venueId: number;
  screen: string;
  seats: ScreenSeat[];
}
