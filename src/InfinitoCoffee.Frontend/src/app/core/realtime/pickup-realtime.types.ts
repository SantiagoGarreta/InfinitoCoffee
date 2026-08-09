import { PickupOrder } from '../pickup/models/pickup-order.model';

export type PickupRealtimeEventName = 'OrderStatusChanged' | 'OrderCancelled';

export interface PickupRealtimeEvent {
  type: PickupRealtimeEventName;
  order: PickupOrder;
}
