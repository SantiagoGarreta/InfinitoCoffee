import { OrderStatus } from './models/order.model';
import { PENDING_DELAY_MINUTES, PREPARING_DELAY_MINUTES } from './order.constants';

export function formatOrderLocalTime(utcDate: string): string {
  return new Intl.DateTimeFormat('es-UY', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(utcDate));
}

export function formatElapsedTime(createdAtUtc: string, now: Date): string {
  const createdAt = new Date(createdAtUtc);
  const diffMilliseconds = Math.max(0, now.getTime() - createdAt.getTime());
  const totalMinutes = Math.floor(diffMilliseconds / 60000);

  if (totalMinutes < 1) {
    return '';
  }

  if (totalMinutes < 60) {
    return `${totalMinutes} min`;
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${hours} h ${minutes} min`;
}

export function isOrderDelayed(status: OrderStatus, createdAtUtc: string, now: Date): boolean {
  const diffMinutes = Math.floor(Math.max(0, now.getTime() - new Date(createdAtUtc).getTime()) / 60000);

  return (status === 'Pending' && diffMinutes > PENDING_DELAY_MINUTES)
    || (status === 'Preparing' && diffMinutes > PREPARING_DELAY_MINUTES);
}

export function compareOrderNumbers(left: string, right: string): number {
  const leftDisplayOrderNumber = toDisplayOrderNumber(left);
  const rightDisplayOrderNumber = toDisplayOrderNumber(right);

  if (leftDisplayOrderNumber !== null && rightDisplayOrderNumber !== null) {
    return leftDisplayOrderNumber - rightDisplayOrderNumber;
  }

  return left.localeCompare(right);
}

function toDisplayOrderNumber(value: string): number | null {
  if (!/^\d+$/.test(value)) {
    return null;
  }

  const displayOrderNumber = Number(value);
  return Number.isInteger(displayOrderNumber) && displayOrderNumber >= 1 && displayOrderNumber <= 99
    ? displayOrderNumber
    : null;
}
