import { OrderRealtimeDto } from '../orders/models/order.model';

export type OrdersRealtimeEventName =
  | 'OrderCreated'
  | 'OrderStatusChanged'
  | 'OrderCancelled';

export interface OrdersRealtimeEvent {
  type: OrdersRealtimeEventName;
  order: OrderRealtimeDto;
}
