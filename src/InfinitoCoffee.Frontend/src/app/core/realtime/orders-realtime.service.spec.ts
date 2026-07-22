import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';

import { OrderRealtimeDto } from '../orders/models/order.model';
import { ORDERS_HUB_CONNECTION_FACTORY, OrdersHubConnection } from './orders-hub-connection';
import { OrdersRealtimeService } from './orders-realtime.service';

class FakeOrdersHubConnection implements OrdersHubConnection {
  private readonly eventHandlers = new Map<string, (order: OrderRealtimeDto) => void>();
  private reconnectingHandler?: (error?: Error) => void;
  private reconnectedHandler?: (connectionId?: string) => void;
  private closeHandler?: (error?: Error) => void;

  startCalls = 0;
  stopCalls = 0;

  on<T>(methodName: string, newMethod: (arg: T) => void): void {
    this.eventHandlers.set(methodName, newMethod as unknown as (order: OrderRealtimeDto) => void);
  }

  off(methodName: string): void {
    this.eventHandlers.delete(methodName);
  }

  start(): Promise<void> {
    this.startCalls++;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stopCalls++;
    return Promise.resolve();
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnectingHandler = callback;
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedHandler = callback;
  }

  onclose(callback: (error?: Error) => void): void {
    this.closeHandler = callback;
  }

  emitOrder(eventName: string, order: OrderRealtimeDto): void {
    this.eventHandlers.get(eventName)?.(order);
  }

  emitReconnecting(): void {
    this.reconnectingHandler?.();
  }

  emitReconnected(): void {
    this.reconnectedHandler?.('connection-1');
  }

  emitClose(): void {
    this.closeHandler?.();
  }
}

describe('OrdersRealtimeService', () => {
  function configure(platformId: object | string = 'browser') {
    const connection = new FakeOrdersHubConnection();

    TestBed.configureTestingModule({
      providers: [
        OrdersRealtimeService,
        {
          provide: PLATFORM_ID,
          useValue: platformId,
        },
        {
          provide: ORDERS_HUB_CONNECTION_FACTORY,
          useValue: () => connection,
        },
      ],
    });

    return {
      service: TestBed.inject(OrdersRealtimeService),
      connection,
    };
  }

  const sampleOrder: OrderRealtimeDto = {
    id: 'order-1',
    orderNumber: 'A-100',
    source: 'Counter',
    status: 'Pending',
    createdAtUtc: '2026-07-22T12:00:00Z',
    startedAtUtc: null,
    readyAtUtc: null,
    deliveredAtUtc: null,
    cancelledAtUtc: null,
    total: 15,
  };

  it('registers the three order events', async () => {
    const { service, connection } = configure();
    const receivedEvents: string[] = [];

    service.subscribe((event) => {
      receivedEvents.push(event.type);
    });

    await service.start();
    connection.emitOrder('OrderCreated', sampleOrder);
    connection.emitOrder('OrderStatusChanged', { ...sampleOrder, status: 'Preparing' });
    connection.emitOrder('OrderCancelled', { ...sampleOrder, status: 'Cancelled', cancelledAtUtc: '2026-07-22T12:05:00Z' });

    expect(receivedEvents).toEqual(['OrderCreated', 'OrderStatusChanged', 'OrderCancelled']);
  });

  it('updates connection state while reconnecting and then reconnecting back', async () => {
    const { service, connection } = configure();
    let resyncRequests = 0;

    service.subscribeToResyncRequested(() => {
      resyncRequests++;
    });

    await service.start();
    expect(service.connectionState()).toBe('connected');

    connection.emitReconnecting();
    expect(service.connectionState()).toBe('reconnecting');

    connection.emitReconnected();
    expect(service.connectionState()).toBe('connected');
    expect(resyncRequests).toBe(1);

    connection.emitClose();
    expect(service.connectionState()).toBe('disconnected');
  });

  it('does not start SignalR during SSR', async () => {
    const { service, connection } = configure('server');

    await service.start();

    expect(connection.startCalls).toBe(0);
    expect(service.connectionState()).toBe('disconnected');
  });
});
