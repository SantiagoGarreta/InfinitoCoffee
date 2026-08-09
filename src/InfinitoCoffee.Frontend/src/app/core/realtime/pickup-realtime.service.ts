import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, isDevMode, signal } from '@angular/core';

import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { PickupOrder } from '../pickup/models/pickup-order.model';
import { HUB_CONNECTION_FACTORY, HubConnectionLike } from './hub-connection';
import { PickupRealtimeEvent, PickupRealtimeEventName } from './pickup-realtime.types';
import { RealtimeConnectionState } from './realtime-connection-state';

type EventListener = (event: PickupRealtimeEvent) => void;
type ResyncListener = () => void;

@Injectable({ providedIn: 'root' })
export class PickupRealtimeService {
  readonly connectionState = signal<RealtimeConnectionState>('disconnected');

  private readonly hubConnectionFactory = inject(HUB_CONNECTION_FACTORY);
  private readonly runtimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly devLoggingEnabled = isDevMode();
  private readonly eventListeners = new Set<EventListener>();
  private readonly resyncListeners = new Set<ResyncListener>();
  private connection: HubConnectionLike | null = null;
  private startPromise: Promise<void> | null = null;

  async start(): Promise<void> {
    if (!this.isBrowser || this.connectionState() === 'connected') {
      return;
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    const connection = this.ensureConnection();
    this.connectionState.set('connecting');
    this.startPromise = connection.start()
      .then(() => this.connectionState.set('connected'))
      .catch((error: unknown) => {
        this.connectionState.set('disconnected');
        this.startPromise = null;
        this.log('connection failed', error);
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

    this.detachHandlers(connection);
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
    return () => this.eventListeners.delete(listener);
  }

  subscribeToResyncRequested(listener: ResyncListener): () => void {
    this.resyncListeners.add(listener);
    return () => this.resyncListeners.delete(listener);
  }

  private ensureConnection(): HubConnectionLike {
    if (this.connection) {
      return this.connection;
    }

    const connection = this.hubConnectionFactory(
      this.runtimeConfig.pickupSignalRHubUrl,
      { withCredentials: false },
    );
    this.registerHandler(connection, 'OrderStatusChanged');
    this.registerHandler(connection, 'OrderCancelled');
    connection.onreconnecting(() => this.connectionState.set('reconnecting'));
    connection.onreconnected(() => {
      this.connectionState.set('connected');
      for (const listener of this.resyncListeners) {
        listener();
      }
    });
    connection.onclose((error) => {
      this.startPromise = null;
      this.connectionState.set('disconnected');
      this.log('connection closed', error);
    });

    this.connection = connection;
    return connection;
  }

  private registerHandler(connection: HubConnectionLike, eventName: PickupRealtimeEventName): void {
    connection.on<PickupOrder>(eventName, (order) => {
      for (const listener of this.eventListeners) {
        listener({ type: eventName, order });
      }
    });
  }

  private detachHandlers(connection: HubConnectionLike): void {
    connection.off('OrderStatusChanged');
    connection.off('OrderCancelled');
  }

  private log(message: string, error?: unknown): void {
    if (this.devLoggingEnabled) {
      console.debug('[PickupRealtimeService]', message, error ?? '');
    }
  }
}
