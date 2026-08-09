import { TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { Order, OrderRealtime } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { KitchenOrdersStore } from './kitchen-orders.store';

class FakeOrdersApiService {
  activeOrders: Order[] = [];
  startPreparationResponse: Order | null = null;

  getActiveOrders(): Promise<Order[]> {
    return Promise.resolve(this.activeOrders);
  }

  startPreparation(): Promise<Order> {
    return Promise.resolve(this.startPreparationResponse ?? this.activeOrders[0]!);
  }

  markReady(): Promise<Order> {
    return Promise.resolve(this.activeOrders[0]!);
  }

  deliver(): Promise<Order> {
    return Promise.resolve(this.activeOrders[0]!);
  }

}

class FakeOrdersRealtimeService {
  private eventListener?: (event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtime }) => void;
  private resyncListener?: () => void;

  readonly connectionState = () => 'connected' as const;
  startCalls = 0;

  start(): Promise<void> {
    this.startCalls++;
    return Promise.resolve();
  }

  restart(): Promise<void> {
    return Promise.resolve();
  }

  stop(): Promise<void> {
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

describe('KitchenOrdersStore', () => {
  function createOrder(orderNumber: string, status: Order['status'], createdAtUtc: string): Order {
    return {
      id: `${orderNumber}-id`,
      orderNumber,
      source: 'Counter',
      status,
      createdAtUtc,
      startedAtUtc: null,
      readyAtUtc: null,
      deliveredAtUtc: null,
      cancelledAtUtc: null,
      notes: null,
      total: 12,
      items: [
        {
          id: `${orderNumber}-item`,
          productId: 'product-1',
          productName: 'Latte',
          unitPrice: 12,
          quantity: 1,
          notes: null,
          lineTotal: 12,
        },
      ],
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        KitchenOrdersStore,
        { provide: OrdersApiService, useClass: FakeOrdersApiService },
        { provide: OrdersRealtimeService, useClass: FakeOrdersRealtimeService },
      ],
    });
  });

  it('orders active cards by createdAt and orderNumber', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;

    api.activeOrders = [
      createOrder('A-200', 'Pending', '2026-07-22T12:02:00Z'),
      createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z'),
      createOrder('A-150', 'Pending', '2026-07-22T12:00:00Z'),
    ];

    await store.initialize();

    expect(store.queuedOrders().map((order) => order.orderNumber)).toEqual(['A-100', 'A-150', 'A-200']);
  });

  it('starts the shared realtime connection during initialization', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;

    await store.initialize();

    expect(realtime.startCalls).toBe(1);
  });

  it('does not keep delivered or cancelled orders visible', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;

    api.activeOrders = [
      createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z'),
      createOrder('A-200', 'Delivered', '2026-07-22T12:01:00Z'),
      createOrder('A-300', 'Cancelled', '2026-07-22T12:02:00Z'),
    ];

    await store.initialize();

    expect(store.orders().map((order) => order.orderNumber)).toEqual(['A-100']);
  });

  it('executes start preparation and updates the matching order', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    api.activeOrders = [createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z')];
    api.startPreparationResponse = {
      ...api.activeOrders[0]!,
      status: 'Preparing',
      startedAtUtc: '2026-07-22T12:03:00Z',
    };

    await store.initialize();
    await store.startPreparation('A-100-id');

    expect(store.preparingOrders().map((order) => order.orderNumber)).toEqual(['A-100']);
    expect(store.activeActionOrderId()).toBeNull();
  });

  it('replaces local state after resync', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.activeOrders = [createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z')];

    await store.initialize();

    api.activeOrders = [createOrder('A-200', 'Preparing', '2026-07-22T12:05:00Z')];
    realtime.requestResync();
    await Promise.resolve();

    expect(store.orders().map((order) => order.orderNumber)).toEqual(['A-200']);
  });
});
