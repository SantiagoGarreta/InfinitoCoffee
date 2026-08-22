import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { HUB_CONNECTION_FACTORY, HubConnectionLike, HubConnectionOptions } from './hub-connection';
import { PickupRealtimeService } from './pickup-realtime.service';

class FakeHubConnection implements HubConnectionLike {
  private readonly handlers = new Map<string, (value: unknown) => void>();
  startCalls = 0;
  stopCalls = 0;
  on<T>(name: string, callback: (value: T) => void): void {
    this.handlers.set(name, callback as (value: unknown) => void);
  }
  off(name: string): void { this.handlers.delete(name); }
  start(): Promise<void> { this.startCalls++; return Promise.resolve(); }
  stop(): Promise<void> { this.stopCalls++; return Promise.resolve(); }
  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}
  emit(name: string, value: unknown): void { this.handlers.get(name)?.(value); }
}

describe('PickupRealtimeService', () => {
  it('uses the public hub without credentials and exposes only public events', async () => {
    const connection = new FakeHubConnection();
    let receivedUrl = '';
    let receivedOptions: HubConnectionOptions | undefined;
    TestBed.configureTestingModule({
      providers: [
        PickupRealtimeService,
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: APP_RUNTIME_CONFIG,
          useValue: {
            apiBaseUrl: 'http://localhost:5165',
            signalRHubUrl: 'http://localhost:5165/hubs/orders',
            pickupSignalRHubUrl: 'http://localhost:5165/hubs/pickup',
          },
        },
        {
          provide: HUB_CONNECTION_FACTORY,
          useValue: (url: string, options: HubConnectionOptions) => {
            receivedUrl = url;
            receivedOptions = options;
            return connection;
          },
        },
      ],
    });
    const service = TestBed.inject(PickupRealtimeService);
    const eventNames: string[] = [];
    service.subscribe((event) => eventNames.push(event.type));

    await service.start();
    connection.emit('OrderStatusChanged', {
      id: 'order-1',
      orderNumber: 'A-100',
      status: 'Preparing',
      createdAtUtc: '2026-08-09T12:00:00Z',
      items: [{ id: 'item-1', productName: 'Jugo de naranja', quantity: 1 }],
    });
    connection.emit('OrderCancelled', {
      id: 'order-1',
      orderNumber: 'A-100',
      status: 'Cancelled',
      createdAtUtc: '2026-08-09T12:00:00Z',
      items: [{ id: 'item-1', productName: 'Jugo de naranja', quantity: 1 }],
    });

    expect(receivedUrl).toBe('http://localhost:5165/hubs/pickup');
    expect(receivedOptions).toEqual({ withCredentials: false });
    expect(eventNames).toEqual(['OrderStatusChanged', 'OrderCancelled']);
  });

  it('does not start during SSR', async () => {
    const connection = new FakeHubConnection();
    TestBed.configureTestingModule({
      providers: [
        PickupRealtimeService,
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: HUB_CONNECTION_FACTORY, useValue: () => connection },
      ],
    });

    await TestBed.inject(PickupRealtimeService).start();

    expect(connection.startCalls).toBe(0);
  });
});
