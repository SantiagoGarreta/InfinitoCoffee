import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, input, output, signal } from '@angular/core';

import { Order } from '../../../core/orders/models/order.model';
import { formatElapsedTime, formatOrderLocalTime, isOrderDelayed } from '../../../core/orders/order-view.utils';

@Component({
  selector: 'app-order-card',
  standalone: true,
  imports: [CommonModule, CurrencyPipe],
  templateUrl: './order-card.component.html',
  styleUrl: './order-card.component.scss',
})
export class OrderCardComponent implements OnInit, OnDestroy {
  readonly order = input.required<Order>();
  readonly isActionPending = input(false);
  readonly actionError = input<string | null>(null);

  readonly startPreparation = output<void>();
  readonly markReady = output<void>();
  readonly deliver = output<void>();

  private readonly now = signal(new Date());
  private timerId: ReturnType<typeof setInterval> | null = null;

  readonly localCreatedAt = computed(() => formatOrderLocalTime(this.order().createdAtUtc));
  readonly elapsedTime = computed(() => formatElapsedTime(this.order().createdAtUtc, this.now()));
  readonly delayed = computed(() => isOrderDelayed(this.order().status, this.order().createdAtUtc, this.now()));
  readonly primaryActionLabel = computed(() => {
    switch (this.order().status) {
      case 'Pending':
        return 'Empezar a preparar';
      case 'Preparing':
        return 'Marcar como listo';
      case 'Ready':
        return 'Entregar pedido';
      default:
        return '';
    }
  });

  ngOnInit(): void {
    this.timerId = setInterval(() => {
      this.now.set(new Date());
    }, 60000);
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  handlePrimaryAction(): void {
    switch (this.order().status) {
      case 'Pending':
        this.startPreparation.emit();
        break;
      case 'Preparing':
        this.markReady.emit();
        break;
      case 'Ready':
        this.deliver.emit();
        break;
      default:
        break;
    }
  }
}
