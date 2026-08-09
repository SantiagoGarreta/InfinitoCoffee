import { UserRole } from './models/authenticated-user.model';

export function getRoleHome(role: UserRole): string {
  return role === 'Kitchen' ? '/kitchen' : '/orders/new';
}
