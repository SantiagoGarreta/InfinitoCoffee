import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';

import { OrdersApiService } from '../orders/data-access/orders-api.service';
import { ORDERS_HUB_CONNECTION_FACTORY, OrdersHubConnection } from './orders-hub-connection';
import { OrdersRealtimeService } from './orders-realtime.service';
import { KitchenOrdersStore } from '../../features/kitchen-display/data-access/kitchen-orders.store';
import { PickupOrdersStore } from '../../features/pickup-display/data-access/pickup-orders.store';

class FakeOrdersHubConnection implements OrdersHubConnection {
  startCalls = 0;
  stopCalls = 0;

  on(): void {}
  off(): void {}

  start(): Promise<void> {
    this.startCalls++;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stopCalls++;
    return Promise.resolve();
  }

  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
}

class FakeOrdersApiService {
  getActiveOrders(): Promise<[]> {
    return Promise.resolve([]);
  }

  getPickupOrders(): Promise<[]> {
    return Promise.resolve([]);
  }

  startPreparation(): Promise<never> {
    throw new Error('Not implemented in this test.');
  }

  markReady(): Promise<never> {
    throw new Error('Not implemented in this test.');
  }

  deliver(): Promise<never> {
    throw new Error('Not implemented in this test.');
  }

  cancel(): Promise<never> {
    throw new Error('Not implemented in this test.');
  }
}

describe('Realtime lifecycle across screens', () => {
  it('keeps the shared SignalR connection alive when navigating from kitchen to pickup', async () => {
    const connection = new FakeOrdersHubConnection();

    TestBed.configureTestingModule({
      providers: [
        KitchenOrdersStore,
        PickupOrdersStore,
        OrdersRealtimeService,
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: OrdersApiService, useClass: FakeOrdersApiService },
        { provide: ORDERS_HUB_CONNECTION_FACTORY, useValue: () => connection },
      ],
    });

    const kitchenStore = TestBed.inject(KitchenOrdersStore);
    const pickupStore = TestBed.inject(PickupOrdersStore);

    await kitchenStore.initialize();
    kitchenStore.destroy();
    await pickupStore.initialize();

    expect(connection.startCalls).toBe(1);
    expect(connection.stopCalls).toBe(0);
  });
});
