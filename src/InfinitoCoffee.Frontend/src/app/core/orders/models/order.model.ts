export type OrderStatus =
  | 'Pending'
  | 'Preparing'
  | 'Ready'
  | 'Delivered'
  | 'Cancelled';

export interface OrderItem {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  notes: string | null;
  lineTotal: number;
}

export interface OrderApiItemDto {
  id: string;
  productId: string;
  productNameSnapshot: string;
  unitPriceSnapshot: number;
  quantity: number;
  notes: string | null;
  lineTotal: number;
}

export interface Order {
  id: string;
  orderNumber: string;
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

export interface OrderApiDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  createdAtUtc: string;
  startedAtUtc: string | null;
  readyAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  notes: string | null;
  total: number;
  items: OrderApiItemDto[];
}

export interface OrderRealtimeItemDto {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  notes: string | null;
  lineTotal: number;
}

export interface OrderRealtimeDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  createdAtUtc: string;
  startedAtUtc: string | null;
  readyAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  notes: string | null;
  total: number;
  items: OrderRealtimeItemDto[];
}

export type OrderDto = OrderApiDto;
export type OrderItemDto = OrderApiItemDto;
export type OrderRealtime = OrderRealtimeDto;

export interface CreateOrderItemRequest {
  productId: string;
  quantity: number;
  notes: string | null;
}

export interface CreateOrderRequest {
  notes: string | null;
  items: CreateOrderItemRequest[];
}
