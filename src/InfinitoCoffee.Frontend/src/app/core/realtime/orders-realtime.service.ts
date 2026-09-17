import { Injectable, PLATFORM_ID, inject, isDevMode, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { OrderRealtimeDto } from '../orders/models/order.model';
import { HUB_CONNECTION_FACTORY, HubConnectionLike } from './hub-connection';
import { OrdersRealtimeEvent, OrdersRealtimeEventName } from './orders-realtime.types';
import { RealtimeConnectionState } from './realtime-connection-state';

type EventListener = (event: OrdersRealtimeEvent) => void;
type ResyncListener = () => void;

@Injectable({ providedIn: 'root' })
export class OrdersRealtimeService {
  readonly connectionState = signal<RealtimeConnectionState>('disconnected');

  private readonly hubConnectionFactory = inject(HUB_CONNECTION_FACTORY);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly eventListeners = new Set<EventListener>();
  private readonly resyncListeners = new Set<ResyncListener>();
  private readonly devLoggingEnabled = isDevMode();

  private connection: HubConnectionLike | null = null;
  private startPromise: Promise<void> | null = null;

  async start(): Promise<void> {
    if (!this.isBrowser) {
      return;
    }

    this.log('start requested', { url: this.appRuntimeConfig.signalRHubUrl, state: this.connectionState() });

    if (this.startPromise) {
      return this.startPromise;
    }

    if (this.connectionState() === 'connected') {
      return;
    }

    const connection = this.ensureConnection();
    this.connectionState.set('connecting');
    this.startPromise = connection.start()
      .then(() => {
        this.connectionState.set('connected');
        this.log('connection started', { url: this.appRuntimeConfig.signalRHubUrl });
      })
      .catch((error: unknown) => {
        this.connectionState.set('disconnected');
        this.startPromise = null;
        this.log('connection failed', { url: this.appRuntimeConfig.signalRHubUrl, error });
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

    this.log('stop requested', { url: this.appRuntimeConfig.signalRHubUrl });
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

  private ensureConnection(): HubConnectionLike {
    if (this.connection) {
      return this.connection;
    }

    const connection = this.hubConnectionFactory(
      this.appRuntimeConfig.signalRHubUrl,
      { withCredentials: true },
    );
    this.registerOrderHandler(connection, 'OrderCreated');
    this.registerOrderHandler(connection, 'OrderStatusChanged');
    this.registerOrderHandler(connection, 'OrderCancelled');
    connection.onreconnecting((error) => {
      this.connectionState.set('reconnecting');
      this.log('onreconnecting', { url: this.appRuntimeConfig.signalRHubUrl, error });
    });
    connection.onreconnected((connectionId) => {
      this.connectionState.set('connected');
      this.log('onreconnected', { url: this.appRuntimeConfig.signalRHubUrl, connectionId });
      this.requestResync();
    });
    connection.onclose((error) => {
      this.startPromise = null;
      this.connectionState.set('disconnected');
      this.log('onclose', { url: this.appRuntimeConfig.signalRHubUrl, error });
    });

    this.connection = connection;
    return connection;
  }

  private registerOrderHandler(
    connection: HubConnectionLike,
    eventName: OrdersRealtimeEventName,
  ): void {
    connection.on<OrderRealtimeDto>(eventName, (order) => {
      this.emit({ type: eventName, order });
    });
  }

  private detachOrderHandlers(connection: HubConnectionLike): void {
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

  private log(message: string, details?: Record<string, unknown>): void {
    if (!this.devLoggingEnabled) {
      return;
    }

    console.debug('[OrdersRealtimeService]', message, details ?? {});
  }
}
