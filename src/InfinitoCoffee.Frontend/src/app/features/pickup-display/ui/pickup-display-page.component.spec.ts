import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { PickupOrder } from '../../../core/pickup/models/pickup-order.model';
import { PickupOrdersStore } from '../data-access/pickup-orders.store';
import { PickupDisplayPageComponent } from './pickup-display-page.component';

class FakePickupOrdersStore {
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly connectionState = signal<'connected' | 'reconnecting' | 'disconnected'>('connected');
  readonly preparingOrders = signal<PickupOrder[]>([createOrder('A-100', 'Preparing')]);
  readonly readyOrders = signal<PickupOrder[]>([createOrder('A-200', 'Ready')]);
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
}

function createOrder(orderNumber: string, status: PickupOrder['status']): PickupOrder {
  return {
    id: `${orderNumber}-id`,
    orderNumber,
    status,
    createdAtUtc: '2026-07-22T12:00:00Z',
    items: [
      {
        id: `${orderNumber}-item-1`,
        productName: 'Jugo de naranja',
        quantity: 1,
      },
    ],
  };
}

describe('PickupDisplayPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PickupDisplayPageComponent],
      providers: [{ provide: PickupOrdersStore, useClass: FakePickupOrdersStore }],
    }).compileComponents();
  });

  it('renders its public fullscreen page without an authenticated shell', () => {
    const fixture = TestBed.createComponent(PickupDisplayPageComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.pickup-page')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-authenticated-sidebar')).toBeNull();
    expect(fixture.nativeElement.querySelector('nav[aria-label="Navegación principal"]')).toBeNull();
  });

  it('shows preparing and ready sections with the right orders', () => {
    const fixture = TestBed.createComponent(PickupDisplayPageComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('En preparacion');
    expect(text).toContain('Listos para retirar');
    expect(text).toContain('A-100');
    expect(text).toContain('A-200');
    expect(text).toContain('1 Jugo de naranja');
  });

  it('initializes on mount and tears down store subscriptions on destroy', () => {
    const fixture = TestBed.createComponent(PickupDisplayPageComponent);
    const store = TestBed.inject(PickupOrdersStore) as unknown as FakePickupOrdersStore;

    fixture.detectChanges();
    fixture.destroy();

    expect(store.initializeCalls).toBe(1);
    expect(store.destroyCalls).toBe(1);
  });

  it('renders the ready section with the emphasized style', () => {
    const fixture = TestBed.createComponent(PickupDisplayPageComponent);
    fixture.detectChanges();

    const readySection = fixture.nativeElement.querySelector('.pickup-section--ready') as HTMLElement;
    expect(readySection).toBeTruthy();
  });
});
