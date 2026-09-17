import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrderStatus } from '../../../core/orders/models/order.model';
import { compareOrderNumbers } from '../../../core/orders/order-view.utils';
import { PickupApiService } from '../../../core/pickup/data-access/pickup-api.service';
import { PickupOrder } from '../../../core/pickup/models/pickup-order.model';
import { PickupRealtimeService } from '../../../core/realtime/pickup-realtime.service';
import { RealtimeConnectionState } from '../../../core/realtime/realtime-connection-state';

@Injectable({ providedIn: 'root' })
export class PickupOrdersStore {
  readonly orders = signal<PickupOrder[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly orderCount = computed(() => this.orders().length);
  readonly connectionState = computed<RealtimeConnectionState>(() => this.realtimeService.connectionState());
  readonly preparingOrders = computed(() => this.filterByStatus('Preparing'));
  readonly readyOrders = computed(() => this.filterByStatus('Ready'));

  private readonly pickupApiService = inject(PickupApiService);
  private readonly realtimeService = inject(PickupRealtimeService);

  private initialized = false;
  private initializePromise: Promise<void> | null = null;
  private unsubscribeRealtime: (() => void) | null = null;
  private unsubscribeResync: (() => void) | null = null;
  private resyncTimerId: ReturnType<typeof setInterval> | null = null;

  async initialize(): Promise<void> {
    if (this.initializePromise) {
      return this.initializePromise;
    }

    if (!this.initialized) {
     this.unsubscribeRealtime = this.realtimeService.subscribe(({ order }) => {
      if (this.shouldDisplay(order.status)) {
      this.upsertOrder(order);
      return;
    }
    this.removeOrder(order.id);
    });;
      this.unsubscribeResync = this.realtimeService.subscribeToResyncRequested(() => {
        void this.reload();
      });
      this.initialized = true;
      this.resyncTimerId = setInterval(() => void this.reload(), 60000);
    }

    this.initializePromise = this.reload()
      .then(() => this.connectRealtime())
      .finally(() => {
        this.initializePromise = null;
      });

    await this.initializePromise;
  }

  destroy(): void {
    this.unsubscribeRealtime?.();
    this.unsubscribeRealtime = null;
    this.unsubscribeResync?.();
    this.unsubscribeResync = null;
    this.initialized = false;
    if (this.resyncTimerId) {
      clearInterval(this.resyncTimerId);
      this.resyncTimerId = null;
    }
    void this.realtimeService.stop();
  }

  async retryConnection(): Promise<void> {
    await this.connectRealtime(true);
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      const orders = await this.pickupApiService.getOrders();
      this.orders.set(this.normalizeOrders(orders));
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar la pantalla de pickup.'));
    } finally {
      this.loading.set(false);
    }
  }

  private async connectRealtime(forceRestart = false): Promise<void> {
    try {
      if (forceRestart) {
        await this.realtimeService.restart();
        return;
      }

      await this.realtimeService.start();
    } catch (error: unknown) {
      console.warn('[PickupOrdersStore] Realtime connection unavailable.', error);
    }
  }

  private upsertOrder(order: PickupOrder): void {
    this.orders.update((currentOrders) => {
      const nextOrders = new Map(currentOrders.map((item) => [item.id, item]));
      nextOrders.set(order.id, order);
      return this.sortOrders([...nextOrders.values()]);
    });
  }

  private removeOrder(orderId: string): void {
    this.orders.update((currentOrders) => currentOrders.filter((order) => order.id !== orderId));
  }

  private normalizeOrders(orders: PickupOrder[]): PickupOrder[] {
    const nextOrders = new Map<string, PickupOrder>();

    for (const order of orders) {
      if (!this.shouldDisplay(order.status)) {
        continue;
      }

      nextOrders.set(order.id, order);
    }

    return this.sortOrders([...nextOrders.values()]);
  }

  private sortOrders(orders: PickupOrder[]): PickupOrder[] {
    return [...orders].sort((left, right) =>
      left.createdAtUtc.localeCompare(right.createdAtUtc)
      || compareOrderNumbers(left.orderNumber, right.orderNumber),
    );
  }

  private filterByStatus(status: OrderStatus): PickupOrder[] {
    return this.orders().filter((order) => order.status === status);
  }

  private shouldDisplay(status: OrderStatus): boolean {
    return status === 'Preparing' || status === 'Ready';
  }
}
