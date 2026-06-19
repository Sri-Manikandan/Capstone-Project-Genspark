export type Role = 'User' | 'Organizer' | 'Admin';

export interface User {
  id: number;
  name: string;
  email: string;
  phone: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export interface UpdateUserRequest {
  name: string;
  phone: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangeEmailRequest {
  newEmail: string;
  password: string;
}

export interface CloseAccountRequest {
  password: string;
}

export interface UserSearchRequest {
  query?: string;
  role?: Role;
  isActive?: boolean;
  page: number;
  pageSize: number;
}
