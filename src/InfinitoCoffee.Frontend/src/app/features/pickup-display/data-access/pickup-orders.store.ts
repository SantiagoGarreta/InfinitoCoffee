import { Injectable, computed, inject, signal } from '@angular/core';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { OrderDto, OrderRealtimeDto } from '../../../core/orders/models/order.model';
import { OrdersRealtimeService } from '../../../core/realtime/orders-realtime.service';
import { RealtimeConnectionState } from '../../../core/realtime/realtime-connection-state';

@Injectable({ providedIn: 'root' })
export class PickupOrdersStore {
  readonly orders = signal<OrderRealtimeDto[]>([]);
  readonly orderCount = computed(() => this.orders().length);
  readonly connectionState = computed<RealtimeConnectionState>(() => this.realtimeService.connectionState());

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

  private async reload(): Promise<void> {
    const orders = await this.ordersApiService.getPickupOrders();
    this.orders.set(this.normalizeOrders(orders));
  }

  private upsertOrder(order: OrderRealtimeDto): void {
    this.orders.update((currentOrders) => {
      const nextOrders = new Map(currentOrders.map((item) => [item.id, item]));
      nextOrders.set(order.id, order);
      return this.sortOrders([...nextOrders.values()]);
    });
  }

  private removeOrder(orderId: string): void {
    this.orders.update((currentOrders) => currentOrders.filter((order) => order.id !== orderId));
  }

  private normalizeOrders(orders: OrderDto[]): OrderRealtimeDto[] {
    const nextOrders = new Map<string, OrderRealtimeDto>();

    for (const order of orders) {
      if (!this.shouldDisplay(order.status)) {
        continue;
      }

      nextOrders.set(order.id, this.toRealtimeOrder(order));
    }

    return this.sortOrders([...nextOrders.values()]);
  }

  private sortOrders(orders: OrderRealtimeDto[]): OrderRealtimeDto[] {
    return [...orders].sort((left, right) =>
      left.createdAtUtc.localeCompare(right.createdAtUtc)
      || left.orderNumber.localeCompare(right.orderNumber),
    );
  }

  private toRealtimeOrder(order: OrderDto): OrderRealtimeDto {
    return {
      id: order.id,
      orderNumber: order.orderNumber,
      source: order.source,
      status: order.status,
      createdAtUtc: order.createdAtUtc,
      startedAtUtc: order.startedAtUtc,
      readyAtUtc: order.readyAtUtc,
      deliveredAtUtc: order.deliveredAtUtc,
      cancelledAtUtc: order.cancelledAtUtc,
      total: order.total,
    };
  }

  private shouldDisplay(status: string): boolean {
    return status === 'Preparing' || status === 'Ready';
  }
}
