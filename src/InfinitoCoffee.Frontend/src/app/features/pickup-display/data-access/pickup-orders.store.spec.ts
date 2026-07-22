import { TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { Order, OrderRealtime } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { PickupOrdersStore } from './pickup-orders.store';

class FakeOrdersApiService {
  pickupOrders: Order[] = [];

  getPickupOrders(): Promise<Order[]> {
    return Promise.resolve(this.pickupOrders);
  }
}

class FakeOrdersRealtimeService {
  private eventListener?: (event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtime }) => void;
  private resyncListener?: () => void;

  readonly connectionState = () => 'connected' as const;

  start(): Promise<void> {
    return Promise.resolve();
  }

  restart(): Promise<void> {
    return Promise.resolve();
  }

  subscribe(listener: (event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtime }) => void): () => void {
    this.eventListener = listener;
    return () => {
      this.eventListener = undefined;
    };
  }

  subscribeToResyncRequested(listener: () => void): () => void {
    this.resyncListener = listener;
    return () => {
      this.resyncListener = undefined;
    };
  }

  emit(event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtime }): void {
    this.eventListener?.(event);
  }

  requestResync(): void {
    this.resyncListener?.();
  }
}

describe('PickupOrdersStore', () => {
  function createOrder(orderNumber: string, status: Order['status']): Order {
    return {
      id: `${orderNumber}-id`,
      orderNumber,
      source: 'Counter',
      status,
      createdAtUtc: '2026-07-22T12:00:00Z',
      startedAtUtc: null,
      readyAtUtc: null,
      deliveredAtUtc: null,
      cancelledAtUtc: null,
      notes: null,
      total: 9,
      items: [],
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PickupOrdersStore,
        { provide: OrdersApiService, useClass: FakeOrdersApiService },
        { provide: OrdersRealtimeService, useClass: FakeOrdersRealtimeService },
      ],
    });
  });

  it('separates preparing and ready orders in different lists', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    api.pickupOrders = [createOrder('A-100', 'Preparing'), createOrder('A-200', 'Ready')];

    await store.initialize();

    expect(store.preparingOrders().map((order) => order.orderNumber)).toEqual(['A-100']);
    expect(store.readyOrders().map((order) => order.orderNumber)).toEqual(['A-200']);
  });

  it('removes delivered and cancelled orders from pickup', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.pickupOrders = [createOrder('A-100', 'Ready')];

    await store.initialize();
    realtime.emit({
      type: 'OrderStatusChanged',
      order: {
        ...createOrder('A-100', 'Delivered'),
        readyAtUtc: '2026-07-22T12:06:00Z',
        deliveredAtUtc: '2026-07-22T12:09:00Z',
      },
    });

    expect(store.orders()).toHaveLength(0);
  });

  it('replaces local state after resync', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.pickupOrders = [createOrder('A-100', 'Ready')];

    await store.initialize();

    api.pickupOrders = [createOrder('A-300', 'Preparing')];
    realtime.requestResync();
    await Promise.resolve();

    expect(store.orders().map((order) => order.orderNumber)).toEqual(['A-300']);
  });
});
