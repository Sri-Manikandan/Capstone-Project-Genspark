export interface TicketTypeDto {
  id: number;
  eventId: number;
  name: string;
  seatType: string;
  price: number;
  totalQuantity: number;
  availableQuantity: number;
  saleStart: string;
  saleEnd: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateTicketTypeRequest {
  eventId: number;
  name: string;
  seatType: string;
  price: number;
  totalQuantity: number;
  saleStart: string;
  saleEnd: string;
}

export interface UpdateTicketTypeRequest {
  name: string;
  seatType: string;
  price: number;
  totalQuantity: number;
  saleStart: string;
  saleEnd: string;
  isActive: boolean;
}
