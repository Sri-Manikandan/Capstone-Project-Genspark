export interface TicketTypeDto {
  id: number;
  screeningId: number;
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
  screeningId: number;
  name: string;
  seatType: string;
  price: number;
  saleStart: string;
  saleEnd: string;
}

export interface UpdateTicketTypeRequest {
  name: string;
  seatType: string;
  price: number;
  saleStart: string;
  saleEnd: string;
  isActive: boolean;
}
