import { OrderStatus } from '../../orders/models/order.model';

export interface PickupOrderItem {
  id: string;
  productName: string;
  quantity: number;
}

export interface PickupOrder {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  createdAtUtc: string;
  items: PickupOrderItem[];
}
