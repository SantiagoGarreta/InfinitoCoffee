export interface OrderItemDto {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  notes: string | null;
  lineTotal: number;
}

export interface OrderDto {
  id: string;
  orderNumber: string;
  source: string;
  status: string;
  createdAtUtc: string;
  startedAtUtc: string | null;
  readyAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  notes: string | null;
  total: number;
  items: OrderItemDto[];
}

export interface OrderRealtimeDto {
  id: string;
  orderNumber: string;
  source: string;
  status: string;
  createdAtUtc: string;
  startedAtUtc: string | null;
  readyAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  total: number;
}
