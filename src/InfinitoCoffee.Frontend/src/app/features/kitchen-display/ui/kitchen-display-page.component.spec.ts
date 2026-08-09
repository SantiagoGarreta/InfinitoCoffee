import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Order } from '../../../core/orders/models/order.model';
import { KitchenOrdersStore } from '../data-access/kitchen-orders.store';
import { KitchenDisplayPageComponent } from './kitchen-display-page.component';

class FakeKitchenOrdersStore {
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly activeActionOrderId = signal<string | null>('A-100-id');
  readonly actionError = signal<{ orderId: string; message: string } | null>({
    orderId: 'A-100-id',
    message: 'No fue posible actualizar el pedido.',
  });
  readonly connectionState = signal<'connected' | 'reconnecting' | 'disconnected'>('connected');
  readonly orderCount = signal(3);
  readonly queuedOrders = signal<Order[]>([createOrder('A-100', 'Pending')]);
  readonly preparingOrders = signal<Order[]>([createOrder('A-200', 'Preparing')]);
  readonly readyOrders = signal<Order[]>([createOrder('A-300', 'Ready')]);
  initializeCalls = 0;
  destroyCalls = 0;

  initialize(): Promise<void> {
    this.initializeCalls++;
    return Promise.resolve();
  }

  destroy(): void {
    this.destroyCalls++;
  }
  retryConnection(): Promise<void> {
    return Promise.resolve();
  }
  startPreparation(): Promise<void> {
    return Promise.resolve();
  }
  markReady(): Promise<void> {
    return Promise.resolve();
  }
  deliver(): Promise<void> {
    return Promise.resolve();
  }
}

function createOrder(orderNumber: string, status: Order['status']): Order {
  return {
    id: `${orderNumber}-id`,
    orderNumber,
    source: 'Counter',
    status,
    createdAtUtc: '2026-07-22T12:00:00Z',
    startedAtUtc: null,
    readyAtUtc: null,
    deliveredAtUtc: null,
    cancelledAtUtc: null,
    notes: 'Sin azucar',
    total: 12,
    items: [
      {
        id: `${orderNumber}-item`,
        productId: 'product-1',
        productName: 'Flat White',
        unitPrice: 12,
        quantity: 1,
        notes: 'Extra hot',
        lineTotal: 12,
      },
    ],
  };
}

describe('KitchenDisplayPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KitchenDisplayPageComponent],
      providers: [{ provide: KitchenOrdersStore, useClass: FakeKitchenOrdersStore }],
    }).compileComponents();
  });

  it('renders the three kitchen columns and current orders', () => {
    const fixture = TestBed.createComponent(KitchenDisplayPageComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('En cola');
    expect(text).toContain('Preparando');
    expect(text).toContain('Listos');
    expect(text).toContain('A-100');
    expect(text).toContain('A-200');
    expect(text).toContain('A-300');
  });

  it('initializes on mount and tears down store subscriptions on destroy', () => {
    const fixture = TestBed.createComponent(KitchenDisplayPageComponent);
    const store = TestBed.inject(KitchenOrdersStore) as unknown as FakeKitchenOrdersStore;

    fixture.detectChanges();
    fixture.destroy();

    expect(store.initializeCalls).toBe(1);
    expect(store.destroyCalls).toBe(1);
  });

  it('disables the button while an action is running and shows the action error', () => {
    const fixture = TestBed.createComponent(KitchenDisplayPageComponent);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.order-card__button--primary') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('No fue posible actualizar el pedido.');
  });
});
