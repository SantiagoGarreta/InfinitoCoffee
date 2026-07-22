import { TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { OrderDto, OrderRealtimeDto } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { PickupOrdersStore } from './pickup-orders.store';

class FakeOrdersApiService {
  pickupOrders: OrderDto[] = [];

  getPickupOrders(): Promise<OrderDto[]> {
    return Promise.resolve(this.pickupOrders);
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

describe('PickupOrdersStore', () => {
  function createOrder(orderNumber: string, status: string): OrderDto {
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

  it('removes delivered and cancelled orders from pickup', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.pickupOrders = [createOrder('A-100', 'Ready')];

    await store.initialize();

    realtime.emit({
      type: 'OrderStatusChanged',
      order: {
        id: 'A-100-id',
        orderNumber: 'A-100',
        source: 'Counter',
        status: 'Delivered',
        createdAtUtc: '2026-07-22T12:00:00Z',
        startedAtUtc: '2026-07-22T12:02:00Z',
        readyAtUtc: '2026-07-22T12:06:00Z',
        deliveredAtUtc: '2026-07-22T12:09:00Z',
        cancelledAtUtc: null,
        total: 9,
      },
    });

    expect(store.orders()).toHaveLength(0);
  });

  it('keeps preparing and ready orders visible', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    api.pickupOrders = [];

    await store.initialize();

    realtime.emit({
      type: 'OrderStatusChanged',
      order: {
        id: 'A-200-id',
        orderNumber: 'A-200',
        source: 'Counter',
        status: 'Preparing',
        createdAtUtc: '2026-07-22T12:04:00Z',
        startedAtUtc: '2026-07-22T12:05:00Z',
        readyAtUtc: null,
        deliveredAtUtc: null,
        cancelledAtUtc: null,
        total: 11,
      },
    });

    expect(store.orders().map((order) => order.orderNumber)).toEqual(['A-200']);
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
