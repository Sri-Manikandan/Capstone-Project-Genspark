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
  screen: string;
  slug: string;
  city: string;
  venueName: string;
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
  screen: string;
}

export interface UpdateEventRequest {
  title: string;
  description: string;
  startTime: string;
  endTime: string;
  imageUrl: string;
  category: string;
  screen: string;
}

export interface OrganizerSummary {
  id: number;
  name: string;
  email: string;
  phone: string;
  memberSince: string;
  isActive: boolean;
  publishedEventCount: number;
  rejectedEventCount: number;
  totalEventCount: number;
}

export interface TicketCategorySummary {
  name: string;
  seatType: string;
  price: number;
  totalQuantity: number;
}

export interface ReviewSignals {
  leadTimeOk: boolean;
  imageUrlValid: boolean;
  descriptionAdequate: boolean;
  hasTicketCategories: boolean;
  pricingSane: boolean;
}

// Enriched payload the admin approval queue renders (mirrors PendingEventReviewDto).
export interface PendingEventReview {
  id: number;
  title: string;
  description: string;
  category: string;
  imageUrl: string;
  startTime: string;
  endTime: string;
  screen: string;
  createdAt: string;
  rejectionReason?: string | null;
  venueId: number;
  venueName: string;
  city: string;
  organizer: OrganizerSummary;
  ticketCategories: TicketCategorySummary[];
  signals: ReviewSignals;
}

export interface EventSearchRequest {
  query?: string;
  category?: string;
  city?: string;
  status?: string;
  startFrom?: string;
  startTo?: string;
  sortBy?: 'title' | 'startTime' | 'createdAt';
  sortOrder?: 'asc' | 'desc';
  page: number;
  pageSize: number;
}
