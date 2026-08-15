import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Order } from '../../../core/orders/models/order.model';
import { OrderCardComponent } from './order-card.component';

describe('OrderCardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [OrderCardComponent] }).compileComponents();
  });

  it('shows cancellation for an active order and returns without emitting', () => {
    const fixture = createFixture();
    const cancelSpy = vi.fn();
    fixture.componentInstance.cancelOrder.subscribe(cancelSpy);

    clickButton(fixture, 'Cancelar pedido');
    expect(fixture.nativeElement.textContent).toContain('¿Cancelar este pedido?');
    expect(fixture.nativeElement.textContent).not.toContain('Empezar a preparar');

    clickButton(fixture, 'Volver');
    expect(fixture.nativeElement.textContent).toContain('Cancelar pedido');
    expect(fixture.nativeElement.textContent).not.toContain('¿Cancelar este pedido?');
    expect(cancelSpy).not.toHaveBeenCalled();
  });

  it('renders cancellation before the primary action in normal state', () => {
    const fixture = createFixture();

    expect(buttonLabels(fixture)).toEqual(['Cancelar pedido', 'Empezar a preparar']);
  });

  it('replaces normal actions with back and confirmation in that order', () => {
    const fixture = createFixture();

    clickButton(fixture, 'Cancelar pedido');

    expect(buttonLabels(fixture)).toEqual(['Volver', 'Confirmar cancelación']);
    expect(fixture.nativeElement.textContent).not.toContain('Empezar a preparar');
    expect(fixture.nativeElement.textContent).not.toContain('Cancelar pedido');
  });

  it('emits cancellation once and disables inline actions while cancelling', () => {
    const fixture = createFixture();
    const cancelSpy = vi.fn();
    fixture.componentInstance.cancelOrder.subscribe(cancelSpy);
    clickButton(fixture, 'Cancelar pedido');

    fixture.componentInstance.confirmCancellation();
    fixture.componentInstance.confirmCancellation();
    fixture.detectChanges();

    expect(cancelSpy).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Cancelando...');
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    expect(buttons).toHaveLength(2);
    expect(buttons.every((button) => button.disabled)).toBe(true);
  });

  it('keeps confirmation and exposes an error so cancellation can be retried', () => {
    const fixture = createFixture();
    const cancelSpy = vi.fn();
    fixture.componentInstance.cancelOrder.subscribe(cancelSpy);
    clickButton(fixture, 'Cancelar pedido');
    fixture.componentInstance.confirmCancellation();

    fixture.componentRef.setInput('actionError', 'El pedido cambió en otro puesto.');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('El pedido cambió en otro puesto.');
    expect(fixture.nativeElement.textContent).toContain('Confirmar cancelación');
    fixture.componentInstance.confirmCancellation();
    expect(cancelSpy).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['Pending', 'startPreparation'],
    ['Preparing', 'markReady'],
    ['Ready', 'deliver'],
  ] as const)('preserves the %s primary action', (status, outputName) => {
    const fixture = createFixture(status);
    const actionSpy = vi.fn();
    fixture.componentInstance[outputName].subscribe(actionSpy);

    fixture.componentInstance.handlePrimaryAction();

    expect(actionSpy).toHaveBeenCalledTimes(1);
  });

  function createFixture(status: Order['status'] = 'Pending'): ComponentFixture<OrderCardComponent> {
    const fixture = TestBed.createComponent(OrderCardComponent);
    fixture.componentRef.setInput('order', createOrder(status));
    fixture.detectChanges();
    return fixture;
  }

  function clickButton(fixture: ComponentFixture<OrderCardComponent>, label: string): void {
    const button = ([...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[])
      .find((candidate) => candidate.textContent?.includes(label));
    expect(button).toBeDefined();
    button!.click();
    fixture.detectChanges();
  }

  function buttonLabels(fixture: ComponentFixture<OrderCardComponent>): string[] {
    return ([...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[])
      .map((button) => button.textContent?.trim() ?? '');
  }

  function createOrder(status: Order['status']): Order {
    return {
      id: 'order-1',
      orderNumber: '23',
      source: 'Counter',
      status,
      createdAtUtc: '2026-08-15T12:00:00Z',
      startedAtUtc: status === 'Pending' ? null : '2026-08-15T12:02:00Z',
      readyAtUtc: status === 'Ready' ? '2026-08-15T12:04:00Z' : null,
      deliveredAtUtc: null,
      cancelledAtUtc: null,
      notes: null,
      total: 150,
      items: [{
        id: 'item-1',
        productId: 'product-1',
        productName: 'Latte',
        unitPrice: 150,
        quantity: 1,
        notes: null,
        lineTotal: 150,
      }],
    };
  }
});
