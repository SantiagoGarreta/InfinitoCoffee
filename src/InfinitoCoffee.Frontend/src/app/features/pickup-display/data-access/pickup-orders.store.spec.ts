import { TestBed } from '@angular/core/testing';

import { PickupApiService } from '../../../core/pickup/data-access/pickup-api.service';
import { PickupOrder } from '../../../core/pickup/models/pickup-order.model';
import { PickupRealtimeService } from '../../../core/realtime/pickup-realtime.service';
import { PickupRealtimeEvent } from '../../../core/realtime/pickup-realtime.types';
import { PickupOrdersStore } from './pickup-orders.store';

class FakePickupApiService {
  orders: PickupOrder[] = [];
  getOrders(): Promise<PickupOrder[]> { return Promise.resolve(this.orders); }
}

class FakePickupRealtimeService {
  private eventListener?: (event: PickupRealtimeEvent) => void;
  private resyncListener?: () => void;
  readonly connectionState = () => 'connected' as const;
  startCalls = 0;
  stopCalls = 0;
  start(): Promise<void> { this.startCalls++; return Promise.resolve(); }
  stop(): Promise<void> { this.stopCalls++; return Promise.resolve(); }
  restart(): Promise<void> { return Promise.resolve(); }
  subscribe(listener: (event: PickupRealtimeEvent) => void): () => void {
    this.eventListener = listener;
    return () => { this.eventListener = undefined; };
  }
  subscribeToResyncRequested(listener: () => void): () => void {
    this.resyncListener = listener;
    return () => { this.resyncListener = undefined; };
  }
  emit(event: PickupRealtimeEvent): void { this.eventListener?.(event); }
  requestResync(): void { this.resyncListener?.(); }
}

describe('PickupOrdersStore', () => {
  function createOrder(orderNumber: string, status: PickupOrder['status']): PickupOrder {
    return {
      id: `${orderNumber}-id`,
      orderNumber,
      status,
      createdAtUtc: '2026-07-22T12:00:00Z',
      items: [
        {
          id: `${orderNumber}-item-1`,
          productName: 'Medialuna',
          quantity: 1,
        },
      ],
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PickupOrdersStore,
        { provide: PickupApiService, useClass: FakePickupApiService },
        { provide: PickupRealtimeService, useClass: FakePickupRealtimeService },
      ],
    });
  });

  it('separates preparing and ready reduced orders', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(PickupApiService) as unknown as FakePickupApiService;
    api.orders = [createOrder('A-100', 'Preparing'), createOrder('A-200', 'Ready')];

    await store.initialize();

    expect(store.preparingOrders().map((order) => order.orderNumber)).toEqual(['A-100']);
    expect(store.readyOrders().map((order) => order.orderNumber)).toEqual(['A-200']);
  });

  it('removes delivered and cancelled orders from pickup', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(PickupApiService) as unknown as FakePickupApiService;
    const realtime = TestBed.inject(PickupRealtimeService) as unknown as FakePickupRealtimeService;
    api.orders = [createOrder('A-100', 'Ready')];
    await store.initialize();

    realtime.emit({ type: 'OrderStatusChanged', order: createOrder('A-100', 'Delivered') });

    expect(store.orders()).toHaveLength(0);
  });

  it('replaces local state after realtime resync', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const api = TestBed.inject(PickupApiService) as unknown as FakePickupApiService;
    const realtime = TestBed.inject(PickupRealtimeService) as unknown as FakePickupRealtimeService;
    api.orders = [createOrder('A-100', 'Ready')];
    await store.initialize();
    api.orders = [createOrder('A-300', 'Preparing')];

    realtime.requestResync();
    await Promise.resolve();

    expect(store.orders().map((order) => order.orderNumber)).toEqual(['A-300']);
  });

  it('stops the public connection on destroy', async () => {
    const store = TestBed.inject(PickupOrdersStore);
    const realtime = TestBed.inject(PickupRealtimeService) as unknown as FakePickupRealtimeService;
    await store.initialize();

    store.destroy();
    await Promise.resolve();

    expect(realtime.stopCalls).toBe(1);
  });
});
