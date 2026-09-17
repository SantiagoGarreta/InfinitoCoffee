import { OrderStatus } from '../../orders/models/order.model';

export interface PickupOrder {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  createdAtUtc: string;
}
