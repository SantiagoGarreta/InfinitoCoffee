import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { Order, OrderStatus } from '../../../core/orders/models/order.model';
import { OrderCardComponent } from './order-card.component';

@Component({
  selector: 'app-kitchen-column',
  standalone: true,
  imports: [CommonModule, OrderCardComponent],
  templateUrl: './kitchen-column.component.html',
  styleUrl: './kitchen-column.component.scss',
})
export class KitchenColumnComponent {
  readonly title = input.required<string>();
  readonly description = input.required<string>();
  readonly orders = input.required<Order[]>();
  readonly activeActionOrderId = input<string | null>(null);
  readonly actionErrorOrderId = input<string | null>(null);
  readonly actionErrorMessage = input<string | null>(null);

  readonly startPreparation = output<string>();
  readonly markReady = output<string>();
  readonly deliver = output<string>();
  readonly cancelOrder = output<string>();

  actionErrorFor(orderId: string): string | null {
    return this.actionErrorOrderId() === orderId ? this.actionErrorMessage() : null;
  }

  isActionPending(orderId: string): boolean {
    return this.activeActionOrderId() === orderId;
  }

  areActionsBlocked(): boolean {
    return this.activeActionOrderId() !== null;
  }

  trackByOrder(index: number, order: Order): string {
    return order.id;
  }
}
