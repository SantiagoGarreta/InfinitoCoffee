import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { OrderRealtimeDto } from '../orders/models/order.model';
import { ORDERS_HUB_CONNECTION_FACTORY, OrdersHubConnection } from './orders-hub-connection';
import { OrdersRealtimeEvent, OrdersRealtimeEventName } from './orders-realtime.types';
import { RealtimeConnectionState } from './realtime-connection-state';

type EventListener = (event: OrdersRealtimeEvent) => void;
type ResyncListener = () => void;

@Injectable({ providedIn: 'root' })
export class OrdersRealtimeService {
  readonly connectionState = signal<RealtimeConnectionState>('disconnected');

  private readonly destroyRef = inject(DestroyRef);
  private readonly hubConnectionFactory = inject(ORDERS_HUB_CONNECTION_FACTORY);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly eventListeners = new Set<EventListener>();
  private readonly resyncListeners = new Set<ResyncListener>();

  private connection: OrdersHubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => {
      void this.stop();
    });
  }

  async start(): Promise<void> {
    if (!this.isBrowser) {
      return;
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    const connection = this.ensureConnection();
    this.connectionState.set('connecting');
    this.startPromise = connection.start()
      .then(() => {
        this.connectionState.set('connected');
      })
      .catch((error: unknown) => {
        this.connectionState.set('disconnected');
        this.startPromise = null;
        throw error;
      });

    await this.startPromise;
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.startPromise = null;

    if (!connection) {
      this.connectionState.set('disconnected');
      return;
    }

    this.detachOrderHandlers(connection);
    this.connection = null;
    this.connectionState.set('disconnected');
    await connection.stop();
  }

  async restart(): Promise<void> {
    await this.stop();
    await this.start();
  }

  subscribe(listener: EventListener): () => void {
    this.eventListeners.add(listener);
    return () => {
      this.eventListeners.delete(listener);
    };
  }

  subscribeToResyncRequested(listener: ResyncListener): () => void {
    this.resyncListeners.add(listener);
    return () => {
      this.resyncListeners.delete(listener);
    };
  }

  private ensureConnection(): OrdersHubConnection {
    if (this.connection) {
      return this.connection;
    }

    const connection = this.hubConnectionFactory(this.appRuntimeConfig.signalRHubUrl);
    this.registerOrderHandler(connection, 'OrderCreated');
    this.registerOrderHandler(connection, 'OrderStatusChanged');
    this.registerOrderHandler(connection, 'OrderCancelled');
    connection.onreconnecting(() => {
      this.connectionState.set('reconnecting');
    });
    connection.onreconnected(() => {
      this.connectionState.set('connected');
      this.requestResync();
    });
    connection.onclose(() => {
      this.startPromise = null;
      this.connectionState.set('disconnected');
    });

    this.connection = connection;
    return connection;
  }

  private registerOrderHandler(
    connection: OrdersHubConnection,
    eventName: OrdersRealtimeEventName,
  ): void {
    connection.on<OrderRealtimeDto>(eventName, (order) => {
      this.emit({ type: eventName, order });
    });
  }

  private detachOrderHandlers(connection: OrdersHubConnection): void {
    connection.off('OrderCreated');
    connection.off('OrderStatusChanged');
    connection.off('OrderCancelled');
  }

  private emit(event: OrdersRealtimeEvent): void {
    for (const listener of this.eventListeners) {
      listener(event);
    }
  }

  private requestResync(): void {
    for (const listener of this.resyncListeners) {
      listener();
    }
  }
}
