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

    clickCancelButton(fixture);
    expect(fixture.nativeElement.textContent).toContain('¿Cancelar este pedido?');
    expect(fixture.nativeElement.textContent).not.toContain('Preparar');

    clickButton(fixture, 'Volver');
    expect(cancelButton(fixture)).toBeDefined();
    expect(fixture.nativeElement.textContent).not.toContain('¿Cancelar este pedido?');
    expect(cancelSpy).not.toHaveBeenCalled();
  });

  it('renders cancellation as an accessible multiplication-sign button outside the footer actions', () => {
    const fixture = createFixture();
    const trigger = cancelButton(fixture);

    expect(trigger).toBeDefined();
    expect(trigger?.tagName).toBe('BUTTON');
    expect(trigger?.getAttribute('aria-label')).toBe('Cancelar pedido');
    expect(trigger?.querySelector('[aria-hidden="true"]')?.textContent).toBe('×');
    expect(trigger?.textContent?.trim()).toBe('×');
    expect(trigger?.textContent).not.toContain('Cancelar');
    expect(fixture.nativeElement.querySelector('.order-card__actions')?.textContent).not.toContain('Cancelar');
  });

  it.each([
    ['Pending', 'Preparar'],
    ['Preparing', 'Listo'],
    ['Ready', 'Entregar'],
  ] as const)('renders the cancellation trigger and %s primary action', (status, primaryLabel) => {
    const fixture = createFixture(status);

    expect(cancelButton(fixture)).toBeDefined();
    expect(actionButtonLabels(fixture)).toEqual([primaryLabel]);
  });

  it.each(['Delivered', 'Cancelled'] as const)('hides cancellation for a %s order', (status) => {
    const fixture = createFixture(status);

    expect(cancelButton(fixture)).toBeUndefined();
  });

  it('replaces normal actions with back and confirmation in that order', () => {
    const fixture = createFixture();

    clickCancelButton(fixture);

    expect(actionButtonLabels(fixture)).toEqual(['Volver', 'Confirmar cancelación']);
    expect(fixture.nativeElement.textContent).not.toContain('Preparar');
    expect(cancelButton(fixture)).toBeUndefined();
  });

  it('emits cancellation once and disables inline actions while cancelling', () => {
    const fixture = createFixture();
    const cancelSpy = vi.fn();
    fixture.componentInstance.cancelOrder.subscribe(cancelSpy);
    clickCancelButton(fixture);

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
    clickCancelButton(fixture);
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

  function clickCancelButton(fixture: ComponentFixture<OrderCardComponent>): void {
    const button = cancelButton(fixture);
    expect(button).toBeDefined();
    button!.click();
    fixture.detectChanges();
  }

  function cancelButton(fixture: ComponentFixture<OrderCardComponent>): HTMLButtonElement | undefined {
    return ([...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[])
      .find((button) => button.getAttribute('aria-label') === 'Cancelar pedido');
  }

  function actionButtonLabels(fixture: ComponentFixture<OrderCardComponent>): string[] {
    return ([...fixture.nativeElement.querySelectorAll('.order-card__actions button')] as HTMLButtonElement[])
      .map((button) => button.textContent?.trim() ?? '');
  }

  function createOrder(status: Order['status']): Order {
    return {
      id: 'order-1',
      orderNumber: '23',
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
