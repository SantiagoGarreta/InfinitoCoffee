export type UserRole = 'Administrator' | 'Cashier' | 'Kitchen';

export interface AuthenticatedUser {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
}
