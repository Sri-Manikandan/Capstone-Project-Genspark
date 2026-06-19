export type EventStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Published'
  | 'Rejected'
  | 'Cancelled';

export interface EventDto {
  id: number;
  organizerId: number;
  venueId: number;
  title: string;
  description: string;
  status: EventStatus;
  rejectionReason?: string | null;
  startTime: string;
  endTime: string;
  imageUrl: string;
  category: string;
  slug: string;
  createdAt: string;
}

export interface CreateEventRequest {
  venueId: number;
  title: string;
  description: string;
  startTime: string;
  endTime: string;
  imageUrl: string;
  category: string;
}

export interface UpdateEventRequest {
  title: string;
  description: string;
  startTime: string;
  endTime: string;
  imageUrl: string;
  category: string;
}

export interface EventSearchRequest {
  query?: string;
  category?: string;
  status?: string;
  startFrom?: string;
  startTo?: string;
  sortBy?: 'title' | 'startTime' | 'createdAt';
  sortOrder?: 'asc' | 'desc';
  page: number;
  pageSize: number;
}
