export type OrderStatus =
  | 'Pending'
  | 'Preparing'
  | 'Ready'
  | 'Delivered'
  | 'Cancelled';

export type OrderSource =
  | 'Counter'
  | 'WhatsApp'
  | 'Web';

export interface OrderItem {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  notes: string | null;
  lineTotal: number;
}

export interface Order {
  id: string;
  orderNumber: string;
  source: OrderSource;
  status: OrderStatus;
  createdAtUtc: string;
  startedAtUtc: string | null;
  readyAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  notes: string | null;
  total: number;
  items: OrderItem[];
}

export interface OrderRealtime {
  id: string;
  orderNumber: string;
  source: OrderSource;
  status: OrderStatus;
  createdAtUtc: string;
  startedAtUtc: string | null;
  readyAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  notes: string | null;
  total: number;
  items: OrderItem[];
}

export type OrderDto = Order;
export type OrderItemDto = OrderItem;
export type OrderRealtimeDto = OrderRealtime;

export interface CreateOrderItemRequest {
  productId: string;
  quantity: number;
  notes: string | null;
}

export interface CreateOrderRequest {
  orderNumber: string;
  source: OrderSource;
  notes: string | null;
  items: CreateOrderItemRequest[];
}
