import { Order, OrderRealtime, OrderSource, OrderStatus } from './models/order.model';

export function toOrderStatus(value: string): OrderStatus {
  return value as OrderStatus;
}

export function toOrderSource(value: string): OrderSource {
  return value as OrderSource;
}

export function toOrder(order: Order): Order {
  return {
    ...order,
    source: toOrderSource(order.source),
    status: toOrderStatus(order.status),
  };
}

export function toRealtimeOrder(order: Order): OrderRealtime {
  return {
    id: order.id,
    orderNumber: order.orderNumber,
    source: order.source,
    status: order.status,
    createdAtUtc: order.createdAtUtc,
    startedAtUtc: order.startedAtUtc,
    readyAtUtc: order.readyAtUtc,
    deliveredAtUtc: order.deliveredAtUtc,
    cancelledAtUtc: order.cancelledAtUtc,
    notes: order.notes,
    total: order.total,
    items: order.items,
  };
}
