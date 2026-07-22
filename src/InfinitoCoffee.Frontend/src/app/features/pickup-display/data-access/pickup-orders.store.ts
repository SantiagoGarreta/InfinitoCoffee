import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { toOrder } from '../../../core/orders/order.mappers';
import { Order, OrderStatus } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { RealtimeConnectionState } from '../../../core/realtime/realtime-connection-state';

@Injectable({ providedIn: 'root' })
export class PickupOrdersStore {
  readonly orders = signal<Order[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly orderCount = computed(() => this.orders().length);
  readonly connectionState = computed<RealtimeConnectionState>(() => this.realtimeService.connectionState());
  readonly preparingOrders = computed(() => this.filterByStatus('Preparing'));
  readonly readyOrders = computed(() => this.filterByStatus('Ready'));

  private readonly ordersApiService = inject(OrdersApiService);
  private readonly realtimeService = inject(OrdersRealtimeService);

  private initialized = false;
  private initializePromise: Promise<void> | null = null;
  private unsubscribeRealtime: (() => void) | null = null;
  private unsubscribeResync: (() => void) | null = null;

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
      });
      this.unsubscribeResync = this.realtimeService.subscribeToResyncRequested(() => {
        void this.reload();
      });
      this.initialized = true;
    }

    this.initializePromise = this.reload()
      .then(() => this.realtimeService.start())
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
  }

  async retryConnection(): Promise<void> {
    await this.realtimeService.restart();
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      const orders = await this.ordersApiService.getPickupOrders();
      this.orders.set(this.normalizeOrders(orders));
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar la pantalla de pickup.'));
    } finally {
      this.loading.set(false);
    }
  }

  private upsertOrder(order: Order): void {
    this.orders.update((currentOrders) => {
      const nextOrders = new Map(currentOrders.map((item) => [item.id, item]));
      nextOrders.set(order.id, order);
      return this.sortOrders([...nextOrders.values()]);
    });
  }

  private removeOrder(orderId: string): void {
    this.orders.update((currentOrders) => currentOrders.filter((order) => order.id !== orderId));
  }

  private normalizeOrders(orders: Order[]): Order[] {
    const nextOrders = new Map<string, Order>();

    for (const order of orders) {
      const normalizedOrder = toOrder(order);

      if (!this.shouldDisplay(normalizedOrder.status)) {
        continue;
      }

      nextOrders.set(normalizedOrder.id, normalizedOrder);
    }

    return this.sortOrders([...nextOrders.values()]);
  }

  private sortOrders(orders: Order[]): Order[] {
    return [...orders].sort((left, right) =>
      left.createdAtUtc.localeCompare(right.createdAtUtc)
      || left.orderNumber.localeCompare(right.orderNumber),
    );
  }

  private filterByStatus(status: OrderStatus): Order[] {
    return this.orders().filter((order) => order.status === status);
  }

  private shouldDisplay(status: OrderStatus): boolean {
    return status === 'Preparing' || status === 'Ready';
  }
}
