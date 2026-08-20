import {
  Order,
  OrderApiDto,
  OrderApiItemDto,
  OrderRealtimeDto,
  OrderRealtimeItemDto,
  OrderStatus,
} from './models/order.model';

export function toOrderStatus(value: string): OrderStatus {
  return value as OrderStatus;
}

export function toOrder(order: Order | OrderApiDto | OrderRealtimeDto): Order {
  return {
    ...order,
    status: toOrderStatus(order.status),
    items: order.items.map(toOrderItem),
  };
}

export function toRealtimeOrder(order: Order): OrderRealtimeDto {
  return {
    id: order.id,
    orderNumber: order.orderNumber,
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

function toOrderItem(item: Order['items'][number] | OrderApiItemDto | OrderRealtimeItemDto): Order['items'][number] {
  if ('productName' in item) {
    return item;
  }

  return {
    id: item.id,
    productId: item.productId,
    productName: item.productNameSnapshot,
    unitPrice: item.unitPriceSnapshot,
    quantity: item.quantity,
    notes: item.notes,
    lineTotal: item.lineTotal,
  };
}
