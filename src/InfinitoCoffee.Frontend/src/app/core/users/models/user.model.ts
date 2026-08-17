import { UserRole } from '../../auth/models/authenticated-user.model';

export interface User {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  isSystemUser: boolean;
}
