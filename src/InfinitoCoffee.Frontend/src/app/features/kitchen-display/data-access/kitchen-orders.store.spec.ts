import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { Order, OrderApiDto, OrderRealtime } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { KitchenOrdersStore } from './kitchen-orders.store';

class FakeOrdersApiService {
  activeOrders: OrderApiDto[] = [];
  startPreparationResponse: OrderApiDto | null = null;
  cancelResponse: OrderApiDto | null = null;
  cancelError: unknown = null;
  cancelCalls: string[] = [];
  cancelPromise: Promise<OrderApiDto> | null = null;

  getActiveOrders(): Promise<OrderApiDto[]> {
    return Promise.resolve(this.activeOrders);
  }

  startPreparation(): Promise<OrderApiDto> {
    return Promise.resolve(this.startPreparationResponse ?? this.activeOrders[0]!);
  }

  markReady(): Promise<OrderApiDto> {
    return Promise.resolve(this.activeOrders[0]!);
  }

  deliver(): Promise<OrderApiDto> {
    return Promise.resolve(this.activeOrders[0]!);
  }

  cancel(orderId: string): Promise<OrderApiDto> {
    this.cancelCalls.push(orderId);

    if (this.cancelError) {
      return Promise.reject(this.cancelError);
    }

    return this.cancelPromise
      ?? Promise.resolve(this.cancelResponse ?? this.activeOrders[0]!);
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
  function createOrder(orderNumber: string, status: Order['status'], createdAtUtc: string): OrderApiDto {
    return {
      id: `${orderNumber}-id`,
      orderNumber,
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
          productNameSnapshot: 'Latte',
          unitPriceSnapshot: 12,
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
    expect(store.preparingOrders()[0]?.items[0]?.productName).toBe('Latte');
    expect(store.activeActionOrderId()).toBeNull();
  });

  it('cancels without optimistic removal, prevents duplicate execution and removes after success', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    api.activeOrders = [createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z')];
    api.cancelResponse = {
      ...api.activeOrders[0]!,
      status: 'Cancelled',
      cancelledAtUtc: '2026-07-22T12:04:00Z',
    };
    let resolveCancel!: (order: OrderApiDto) => void;
    api.cancelPromise = new Promise<OrderApiDto>((resolve) => { resolveCancel = resolve; });
    await store.initialize();

    const firstCancellation = store.cancelOrder('A-100-id');
    const duplicateCancellation = store.cancelOrder('A-100-id');

    expect(api.cancelCalls).toEqual(['A-100-id']);
    expect(store.orders()).toHaveLength(1);
    expect(store.activeActionOrderId()).toBe('A-100-id');

    resolveCancel(api.cancelResponse!);
    await Promise.all([firstCancellation, duplicateCancellation]);

    expect(store.orders()).toHaveLength(0);
    expect(store.activeActionOrderId()).toBeNull();
  });

  it('keeps a failed cancellation visible, exposes ProblemDetails and allows retry', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    api.activeOrders = [createOrder('A-100', 'Preparing', '2026-07-22T12:00:00Z')];
    api.cancelError = new HttpErrorResponse({
      status: 409,
      error: { detail: 'El pedido fue modificado por otro puesto.' },
    });
    await store.initialize();

    await store.cancelOrder('A-100-id');

    expect(store.orders()).toHaveLength(1);
    expect(store.actionError()).toEqual({
      orderId: 'A-100-id',
      message: 'El pedido fue modificado por otro puesto.',
    });

    api.cancelError = null;
    api.cancelResponse = {
      ...api.activeOrders[0]!,
      status: 'Cancelled',
      cancelledAtUtc: '2026-07-22T12:05:00Z',
    };
    await store.cancelOrder('A-100-id');

    expect(api.cancelCalls).toEqual(['A-100-id', 'A-100-id']);
    expect(store.orders()).toHaveLength(0);
    expect(store.actionError()).toBeNull();
  });

  it('removes a realtime cancellation idempotently', async () => {
    const store = TestBed.inject(KitchenOrdersStore);
    const api = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const realtime = TestBed.inject(OrdersRealtimeService) as unknown as FakeOrdersRealtimeService;
    const pending = createOrder('A-100', 'Pending', '2026-07-22T12:00:00Z');
    const cancelled: OrderRealtime = {
      ...pending,
      status: 'Cancelled' as const,
      cancelledAtUtc: '2026-07-22T12:04:00Z',
      items: pending.items.map((item) => ({
        id: item.id,
        productId: item.productId,
        productName: item.productNameSnapshot,
        unitPrice: item.unitPriceSnapshot,
        quantity: item.quantity,
        notes: item.notes,
        lineTotal: item.lineTotal,
      })),
    };
    api.activeOrders = [pending];
    await store.initialize();

    realtime.emit({ type: 'OrderCancelled', order: cancelled });
    realtime.emit({ type: 'OrderCancelled', order: cancelled });

    expect(store.orders()).toHaveLength(0);
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
