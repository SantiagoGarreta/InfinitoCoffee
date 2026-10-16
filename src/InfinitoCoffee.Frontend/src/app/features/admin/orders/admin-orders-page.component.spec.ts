import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { OrderApiDto, OrderStatus } from '../../../core/orders/models/order.model';
import { AdminOrdersPageComponent } from './admin-orders-page.component';

describe('AdminOrdersPageComponent', () => {
  const api = {
    getOrderHistory: vi.fn(),
    voidOrder: vi.fn(),
  };
  let fixture: ComponentFixture<AdminOrdersPageComponent>;

  beforeEach(async () => {
    api.getOrderHistory.mockReset().mockResolvedValue({
      items: [order('delivered', 'Delivered'), order('cancelled', 'Cancelled'), order('voided', 'Voided')],
      nextCursor: 'next',
    });
    api.voidOrder.mockReset();
    await TestBed.configureTestingModule({
      imports: [AdminOrdersPageComponent],
      providers: [{ provide: OrdersApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOrdersPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads twenty at first, renders details and only offers void for delivered orders', () => {
    expect(api.getOrderHistory).toHaveBeenCalledWith(20);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Cancelado');
    expect(text).toContain('Anulado');
    expect(text).toContain('2 × Latte');
    expect(fixture.nativeElement.querySelectorAll('.danger-outline')).toHaveLength(1);
    expect(text).toContain('Cargar más');
  });

  it('loads the next cursor and appends orders', async () => {
    api.getOrderHistory.mockResolvedValueOnce({ items: [order('more', 'Pending')], nextCursor: null });

    await fixture.componentInstance.loadMore();

    expect(api.getOrderHistory).toHaveBeenLastCalledWith(20, 'next');
    expect(fixture.componentInstance.orders()).toHaveLength(4);
  });

  it('confirms, blocks duplicate submit and updates the order without removing it', async () => {
    let resolve!: (value: OrderApiDto) => void;
    api.voidOrder.mockReturnValue(new Promise<OrderApiDto>(done => { resolve = done; }));
    const delivered = fixture.componentInstance.orders()[0];
    fixture.componentInstance.askToVoid(delivered);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Esta acción revertirá la venta');

    const first = fixture.componentInstance.voidOrder(delivered);
    const second = fixture.componentInstance.voidOrder(delivered);
    expect(api.voidOrder).toHaveBeenCalledTimes(1);
    resolve({ ...delivered, status: 'Voided', voidedAtUtc: '2026-10-14T13:00:00Z' });
    await Promise.all([first, second]);

    expect(fixture.componentInstance.orders()).toHaveLength(3);
    expect(fixture.componentInstance.orders()[0].status).toBe('Voided');
  });

  it('Volver closes confirmation without calling the API', () => {
    fixture.componentInstance.askToVoid(fixture.componentInstance.orders()[0]);
    fixture.componentInstance.closeConfirmation();
    expect(fixture.componentInstance.confirmingId()).toBeNull();
    expect(api.voidOrder).not.toHaveBeenCalled();
  });

  it('turns a 409 into the required message and local voided state', async () => {
    api.voidOrder.mockRejectedValue(new HttpErrorResponse({ status: 409 }));
    const delivered = fixture.componentInstance.orders()[0];

    await fixture.componentInstance.voidOrder(delivered);

    expect(fixture.componentInstance.error()).toBe('El pedido ya fue anulado.');
    expect(fixture.componentInstance.orders()[0].status).toBe('Voided');
  });

  function order(id: string, status: OrderStatus): OrderApiDto {
    return {
      id, orderNumber: id, status, createdAtUtc: '2026-10-14T12:00:00Z',
      startedAtUtc: null, readyAtUtc: null, deliveredAtUtc: status === 'Delivered' ? '2026-10-14T12:10:00Z' : null,
      cancelledAtUtc: status === 'Cancelled' ? '2026-10-14T12:05:00Z' : null,
      voidedAtUtc: status === 'Voided' ? '2026-10-14T12:15:00Z' : null,
      notes: null, total: 100,
      items: [{ id: `${id}-item`, productId: 'product', productNameSnapshot: 'Latte', unitPriceSnapshot: 50,
        quantity: 2, notes: null, lineTotal: 100 }],
    };
  }
});
