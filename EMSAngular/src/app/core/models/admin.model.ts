export interface OrganizerRequestDto {
  id: number;
  userId: number;
  userName: string;
  userEmail: string;
  status: string;
  reason?: string | null;
  requestedAt: string;
  reviewedAt?: string | null;
  reviewedByAdminId?: number | null;
}

export interface ReviewRequest {
  reason?: string;
}

export interface OrganizerRequestQueryRequest {
  status?: string;
  page: number;
  pageSize: number;
}
