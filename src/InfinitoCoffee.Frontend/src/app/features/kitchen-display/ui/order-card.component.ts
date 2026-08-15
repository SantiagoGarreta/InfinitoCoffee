import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, effect, input, output, signal } from '@angular/core';

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
  readonly actionsBlocked = input(false);
  readonly actionError = input<string | null>(null);

  readonly startPreparation = output<void>();
  readonly markReady = output<void>();
  readonly deliver = output<void>();
  readonly cancelOrder = output<void>();

  readonly confirmingCancellation = signal(false);
  readonly cancelSubmissionStarted = signal(false);

  private readonly now = signal(new Date());
  private timerId: ReturnType<typeof setInterval> | null = null;

  readonly localCreatedAt = computed(() => formatOrderLocalTime(this.order().createdAtUtc));
  readonly elapsedTime = computed(() => formatElapsedTime(this.order().createdAtUtc, this.now()));
  readonly delayed = computed(() => isOrderDelayed(this.order().status, this.order().createdAtUtc, this.now()));
  readonly cancellationPending = computed(() => this.cancelSubmissionStarted() || this.isActionPending());
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

  constructor() {
    effect(() => {
      if (this.actionError()) {
        this.cancelSubmissionStarted.set(false);
      }
    });
  }

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
    if (this.actionsBlocked() || this.confirmingCancellation()) {
      return;
    }

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

  beginCancellation(): void {
    if (this.actionsBlocked()) {
      return;
    }

    this.confirmingCancellation.set(true);
  }

  closeCancellation(): void {
    if (this.cancellationPending() || this.actionsBlocked()) {
      return;
    }

    this.confirmingCancellation.set(false);
  }

  confirmCancellation(): void {
    if (!this.confirmingCancellation() || this.cancellationPending() || this.actionsBlocked()) {
      return;
    }

    this.cancelSubmissionStarted.set(true);
    this.cancelOrder.emit();
  }
}
