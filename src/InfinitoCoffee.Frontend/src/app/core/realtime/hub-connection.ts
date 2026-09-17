import { InjectionToken } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';

export interface HubConnectionLike {
  on<T>(methodName: string, newMethod: (arg: T) => void): void;
  off(methodName: string): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

export interface HubConnectionOptions {
  withCredentials: boolean;
}

export type HubConnectionFactory = (
  hubUrl: string,
  options: HubConnectionOptions,
) => HubConnectionLike;

function createHubConnection(hubUrl: string, options: HubConnectionOptions): HubConnectionLike {
  return new HubConnectionBuilder()
    .withUrl(hubUrl, { withCredentials: options.withCredentials })
    .withAutomaticReconnect()
    .build();
}

export const HUB_CONNECTION_FACTORY = new InjectionToken<HubConnectionFactory>(
  'HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => createHubConnection,
  },
);
