export interface ScreeningDto {
  id: number;
  eventId: number;
  screen: string;
  startTime: string;
  endTime: string;
  status: string;
  createdAt: string;
  isSoldOut: boolean;
}

export interface CreateScreeningRequest {
  eventId: number;
  screen: string;
  startTime: string;
  endTime: string;
}

export interface UpdateScreeningRequest {
  screen: string;
  startTime: string;
  endTime: string;
}
