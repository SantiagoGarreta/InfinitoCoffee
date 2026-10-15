import { CommonModule, isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { OrderApiDto, OrderStatus } from '../../../core/orders/models/order.model';
import { ErrorMessageComponent } from '../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state/loading-state.component';

@Component({
  selector: 'app-admin-orders-page',
  standalone: true,
  imports: [CommonModule, ErrorMessageComponent, LoadingStateComponent],
  templateUrl: './admin-orders-page.component.html',
  styleUrl: './admin-orders-page.component.scss',
})
export class AdminOrdersPageComponent implements OnInit {
  private readonly api = inject(OrdersApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly orders = signal<OrderApiDto[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly error = signal<string | null>(null);
  readonly confirmingId = signal<string | null>(null);
  readonly voidingId = signal<string | null>(null);

  ngOnInit(): void {
    if (this.isBrowser) void this.reload();
    else this.loading.set(false);
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await this.api.getOrderHistory(20);
      this.orders.set(page.items);
      this.nextCursor.set(page.nextCursor);
    } catch (error) {
      this.error.set(toUserMessage(error, 'No fue posible cargar el historial de pedidos.'));
    } finally {
      this.loading.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.error.set(null);
    try {
      const page = await this.api.getOrderHistory(20, cursor);
      this.orders.update(orders => [...orders, ...page.items]);
      this.nextCursor.set(page.nextCursor);
    } catch (error) {
      this.error.set(toUserMessage(error, 'No fue posible cargar más pedidos.'));
    } finally {
      this.loadingMore.set(false);
    }
  }

  askToVoid(order: OrderApiDto): void {
    if (order.status === 'Delivered' && !this.voidingId()) this.confirmingId.set(order.id);
  }

  closeConfirmation(): void {
    if (!this.voidingId()) this.confirmingId.set(null);
  }

  async voidOrder(order: OrderApiDto): Promise<void> {
    if (order.status !== 'Delivered' || this.voidingId()) return;
    this.voidingId.set(order.id);
    this.error.set(null);
    try {
      const updated = await this.api.voidOrder(order.id);
      this.replaceOrder(updated);
      this.confirmingId.set(null);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.replaceOrder({ ...order, status: 'Voided' });
        this.confirmingId.set(null);
        this.error.set('El pedido ya fue anulado.');
      } else {
        this.error.set(toUserMessage(error, 'No fue posible anular el pedido.'));
      }
    } finally {
      this.voidingId.set(null);
    }
  }

  statusLabel(status: OrderStatus): string {
    return ({
      Pending: 'Pendiente', Preparing: 'En preparación', Ready: 'Listo', Delivered: 'Entregado',
      Cancelled: 'Cancelado', Voided: 'Anulado',
    } as Record<OrderStatus, string>)[status];
  }

  itemCount(order: OrderApiDto): number {
    return order.items.reduce((total, item) => total + item.quantity, 0);
  }

  private replaceOrder(updated: OrderApiDto): void {
    this.orders.update(orders => orders.map(order => order.id === updated.id ? updated : order));
  }
}
