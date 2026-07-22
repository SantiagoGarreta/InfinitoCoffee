import { TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { OrderDto, OrderRealtimeDto } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { KitchenOrdersStore } from './kitchen-orders.store';

class FakeOrdersApiService {
  activeOrders: OrderDto[] = [];

  getActiveOrders(): Promise<OrderDto[]> {
    return Promise.resolve(this.activeOrders);
  }
}

class FakeOrdersRealtimeService {
  private eventListener?: (event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtimeDto }) => void;
  private resyncListener?: () => void;

  readonly connectionState = () => 'disconnected' as const;

  start(): Promise<void> {
    return Promise.resolve();
  }

  subscribe(listener: (event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtimeDto }) => void): () => void {
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

  emit(event: { type: 'OrderCreated' | 'OrderStatusChanged' | 'OrderCancelled'; order: OrderRealtimeDto }): void {
    this.eventListener?.(event);
  }

  requestResync(): void {
    this.resyncListener?.();
  }
}

describe('KitchenOrdersStore', () => {
  function createOrder(orderNumber: string, status: string, createdAtUtc: string): OrderDto {
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
      items: [],
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        KitchenOrdersStore,
        {
          provide: OrdersApiService,
          useClass: FakeOrdersApiService,
        },
        {
          provide: OrdersRealtimeService,
          useClass: FakeOrdersRealtimeService,
        },
      ],
    });
  });

  it('avoids duplicates by order id', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.activeOrders = [createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z')];

    await store.initialize();
    realtime.emit({
      type: 'OrderCreated',
      order: {
        id: 'A-100-id',
        orderNumber: 'A-100',
        source: 'Counter',
        status: 'Pending',
        createdAtUtc: '2026-07-22T12:00:00Z',
        startedAtUtc: null,
        readyAtUtc: null,
        deliveredAtUtc: null,
        cancelledAtUtc: null,
        total: 12,
      },
    });

    expect(store.orders()).toHaveLength(1);
  });

  it('updates order status from realtime events', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.activeOrders = [createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z')];

    await store.initialize();
    realtime.emit({
      type: 'OrderStatusChanged',
      order: {
        id: 'A-100-id',
        orderNumber: 'A-100',
        source: 'Counter',
        status: 'Preparing',
        createdAtUtc: '2026-07-22T12:00:00Z',
        startedAtUtc: '2026-07-22T12:03:00Z',
        readyAtUtc: null,
        deliveredAtUtc: null,
        cancelledAtUtc: null,
        total: 12,
      },
    });

    expect(store.orders()[0]?.status).toBe('Preparing');
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
