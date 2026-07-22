import { InjectionToken } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';

export interface OrdersHubConnection {
  on<T>(methodName: string, newMethod: (arg: T) => void): void;
  off(methodName: string): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

export type OrdersHubConnectionFactory = (hubUrl: string) => OrdersHubConnection;

function createOrdersHubConnection(hubUrl: string): OrdersHubConnection {
  return new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .build();
}

export const ORDERS_HUB_CONNECTION_FACTORY = new InjectionToken<OrdersHubConnectionFactory>(
  'ORDERS_HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => createOrdersHubConnection,
  },
);
