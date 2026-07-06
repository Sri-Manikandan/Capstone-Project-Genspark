export type BookingStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Attended';

export interface BookingItemRequest {
  ticketTypeId: number;
  seatId: number;
}

export interface CreateBookingRequest {
  screeningId: number;
  items: BookingItemRequest[];
}

export interface BookingItemDto {
  id: number;
  ticketTypeId: number;
  ticketTypeName: string;
  seatId: number;
  seatLabel: string;
  unitPrice: number;
  ticketStatus: string;
}

export interface BookingDto {
  id: number;
  userId: number;
  screeningId: number;
  eventId: number;
  eventTitle: string;
  screen: string;
  screeningStartTime: string;
  bookingReference: string;
  qrCode: string;
  totalAmount: number;
  bookingStatus: BookingStatus;
  expiresAt: string;
  createdAt: string;
  items: BookingItemDto[];
}

export interface BookingQueryRequest {
  status?: BookingStatus;
  page: number;
  pageSize: number;
}

export interface ValidateQrRequest {
  qrPayload: string;
}
