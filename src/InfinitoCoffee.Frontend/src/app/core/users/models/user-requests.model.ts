import { UserRole } from '../../auth/models/authenticated-user.model';

export interface CreateUserRequest {
  username: string;
  displayName: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  username: string;
  displayName: string;
  role: UserRole;
}

export interface ResetUserPasswordRequest {
  newPassword: string;
}
