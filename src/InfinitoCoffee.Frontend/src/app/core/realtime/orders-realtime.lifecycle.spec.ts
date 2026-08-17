import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../orders/data-access/orders-api.service';
import { PickupApiService } from '../pickup/data-access/pickup-api.service';
import { KitchenOrdersStore } from '../../features/kitchen-display/data-access/kitchen-orders.store';
import { PickupOrdersStore } from '../../features/pickup-display/data-access/pickup-orders.store';
import { HUB_CONNECTION_FACTORY, HubConnectionLike } from './hub-connection';
import { OrdersRealtimeService } from './orders-realtime.service';
import { PickupRealtimeService } from './pickup-realtime.service';

class FakeHubConnection implements HubConnectionLike {
  startCalls = 0;
  stopCalls = 0;
  on(): void {}
  off(): void {}
  start(): Promise<void> { this.startCalls++; return Promise.resolve(); }
  stop(): Promise<void> { this.stopCalls++; return Promise.resolve(); }
  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
}

describe('Realtime lifecycle across screens', () => {
  it('uses and stops independent private and public connections', async () => {
    const privateConnection = new FakeHubConnection();
    const publicConnection = new FakeHubConnection();

    TestBed.configureTestingModule({
      providers: [
        KitchenOrdersStore,
        PickupOrdersStore,
        OrdersRealtimeService,
        PickupRealtimeService,
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: OrdersApiService, useValue: { getActiveOrders: () => Promise.resolve([]) } },
        { provide: PickupApiService, useValue: { getOrders: () => Promise.resolve([]) } },
        {
          provide: HUB_CONNECTION_FACTORY,
          useValue: (url: string) => url.endsWith('/pickup') ? publicConnection : privateConnection,
        },
      ],
    });

    const kitchenStore = TestBed.inject(KitchenOrdersStore);
    const pickupStore = TestBed.inject(PickupOrdersStore);
    await kitchenStore.initialize();
    kitchenStore.destroy();
    await pickupStore.initialize();
    pickupStore.destroy();
    await Promise.resolve();

    expect(privateConnection.startCalls).toBe(1);
    expect(privateConnection.stopCalls).toBe(1);
    expect(publicConnection.startCalls).toBe(1);
    expect(publicConnection.stopCalls).toBe(1);
  });
});
