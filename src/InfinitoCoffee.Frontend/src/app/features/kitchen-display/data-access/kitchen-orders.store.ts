import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { toOrder } from '../../../core/orders/order.mappers';
import { Order, OrderApiDto, OrderRealtimeDto, OrderStatus } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { RealtimeConnectionState } from '../../../core/realtime/realtime-connection-state';

@Injectable({ providedIn: 'root' })
export class KitchenOrdersStore {
  readonly orders = signal<Order[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly activeActionOrderId = signal<string | null>(null);
  readonly actionError = signal<{ orderId: string; message: string } | null>(null);
  readonly orderCount = computed(() => this.orders().length);
  readonly connectionState = computed<RealtimeConnectionState>(() => this.realtimeService.connectionState());
  readonly queuedOrders = computed(() => this.filterByStatus('Pending'));
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
      this.unsubscribeRealtime = this.realtimeService.subscribe(({ type, order }) => {
        if (type === 'OrderCancelled') {
          this.removeOrder(order.id);
          return;
        }

        this.upsertIfActive(toOrder(order));
      });
      this.unsubscribeResync = this.realtimeService.subscribeToResyncRequested(() => {
        void this.reload();
      });
      this.initialized = true;
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
  }

  async retryConnection(): Promise<void> {
    await this.connectRealtime(true);
  }

  async startPreparation(orderId: string): Promise<void> {
    await this.runOrderAction(orderId, () => this.ordersApiService.startPreparation(orderId));
  }

  async markReady(orderId: string): Promise<void> {
    await this.runOrderAction(orderId, () => this.ordersApiService.markReady(orderId));
  }

  async deliver(orderId: string): Promise<void> {
    await this.runOrderAction(orderId, () => this.ordersApiService.deliver(orderId));
  }

  async cancel(orderId: string): Promise<void> {
    await this.runOrderAction(orderId, () => this.ordersApiService.cancel(orderId));
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      const orders = await this.ordersApiService.getActiveOrders();
      this.orders.set(this.normalizeOrders(orders));
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar las comandas activas.'));
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
      console.warn('[KitchenOrdersStore] Realtime connection unavailable.', error);
    }
  }

  private async runOrderAction(
    orderId: string,
    action: () => Promise<Order | OrderApiDto | OrderRealtimeDto>,
  ): Promise<void> {
    this.activeActionOrderId.set(orderId);
    this.actionError.set(null);

    try {
      const updatedOrder = toOrder(await action());
      this.upsertIfActive(updatedOrder);
    } catch (error: unknown) {
      this.actionError.set({
        orderId,
        message: toUserMessage(error, 'No fue posible actualizar el pedido.'),
      });
    } finally {
      this.activeActionOrderId.set(null);
    }
  }

  private upsertIfActive(order: Order): void {
    if (!this.isActive(order.status)) {
      this.removeOrder(order.id);
      return;
    }

    this.orders.update((currentOrders) => {
      const nextOrders = new Map(currentOrders.map((item) => [item.id, item]));
      nextOrders.set(order.id, order);
      return this.sortOrders([...nextOrders.values()]);
    });
  }

  private removeOrder(orderId: string): void {
    this.orders.update((currentOrders) => currentOrders.filter((order) => order.id !== orderId));
  }

  private normalizeOrders(orders: ReadonlyArray<Order | OrderApiDto | OrderRealtimeDto>): Order[] {
    const nextOrders = new Map<string, Order>();

    for (const order of orders) {
      const normalizedOrder = toOrder(order);

      if (!this.isActive(normalizedOrder.status)) {
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

  private isActive(status: OrderStatus): boolean {
    return status !== 'Delivered' && status !== 'Cancelled';
  }
}
