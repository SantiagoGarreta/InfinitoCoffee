import { TestBed } from '@angular/core/testing';

import { OrdersApiService } from '../../../../core/orders/data-access/orders-api.service';
import { OrderResults, OrderResultsGroupBy } from '../../../../core/orders/models/order-results.model';
import { AdminResultsPageComponent } from './admin-results-page.component';

class FakeOrdersApiService {
  response: OrderResults = createResponse('Daily');
  shouldFail = false;
  calls: OrderResultsGroupBy[] = [];

  async getOrderSummary(groupBy: OrderResultsGroupBy = 'Daily'): Promise<OrderResults> {
    this.calls.push(groupBy);
    if (this.shouldFail) {
      throw new Error('offline');
    }

    return {
      ...this.response,
      groupBy,
    };
  }
}

describe('AdminResultsPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminResultsPageComponent],
      providers: [{ provide: OrdersApiService, useClass: FakeOrdersApiService }],
    }).compileComponents();
  });

  it('renders grouped period metrics, operational snapshot and history', async () => {
    const fixture = TestBed.createComponent(AdminResultsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Resultado');
    expect(text).toContain('Periodo actual');
    expect(text).toContain('Comparacion con el periodo anterior');
    expect(text).toContain('Operacion actual');
    expect(text).toContain('Tendencia historica');
    expect(text).toContain('Productos mas vendidos');
    expect(text).toContain('Latte');
    expect(text).toContain('Cookie');
  });

  it('reloads data when the grouping changes', async () => {
    const service = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    const fixture = TestBed.createComponent(AdminResultsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const weeklyButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find(button => (button.textContent ?? '').includes('Semanal')) as HTMLButtonElement;

    weeklyButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.calls).toEqual(['Daily', 'Weekly']);
  });

  it('shows retryable load errors', async () => {
    const service = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    service.shouldFail = true;

    const fixture = TestBed.createComponent(AdminResultsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No fue posible cargar el resultado de ventas');

    service.shouldFail = false;
    (fixture.nativeElement.querySelector('app-error-message button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.calls).toEqual(['Daily', 'Daily']);
    expect(fixture.nativeElement.textContent).toContain('Ingresos del periodo');
  });
});

function createResponse(groupBy: OrderResultsGroupBy): OrderResults {
  return {
    groupBy,
    periodStartDate: '2026-08-20',
    periodEndDate: '2026-08-20',
    currentPeriod: {
      startDate: '2026-08-20',
      endDate: '2026-08-20',
      totalRevenue: 1290,
      totalCost: 540,
      totalProfit: 750,
      totalOrdersCount: 9,
      deliveredOrdersCount: 6,
      cancelledOrdersCount: 1,
      deliveredItemsCount: 14,
      averageDeliveredOrderTotal: 215,
    },
    previousPeriod: {
      startDate: '2026-08-19',
      endDate: '2026-08-19',
      totalRevenue: 980,
      totalCost: 430,
      totalProfit: 550,
      totalOrdersCount: 8,
      deliveredOrdersCount: 5,
      cancelledOrdersCount: 0,
      deliveredItemsCount: 11,
      averageDeliveredOrderTotal: 196,
    },
    operationalSnapshot: {
      totalOrdersCount: 42,
      activeOrdersCount: 3,
      pendingOrdersCount: 1,
      preparingOrdersCount: 1,
      readyOrdersCount: 1,
      deliveredOrdersCount: 36,
      cancelledOrdersCount: 3,
    },
    topSellingProducts: [
      { productId: 'latte', productName: 'Latte', quantitySold: 8, revenue: 720 },
      { productId: 'cookie', productName: 'Cookie', quantitySold: 6, revenue: 570 },
    ],
    history: [
      {
        startDate: '2026-08-18',
        endDate: '2026-08-18',
        totalRevenue: 820,
        totalCost: 340,
        totalProfit: 480,
        totalOrdersCount: 7,
        deliveredOrdersCount: 4,
        cancelledOrdersCount: 1,
        deliveredItemsCount: 10,
        averageDeliveredOrderTotal: 205,
      },
      {
        startDate: '2026-08-19',
        endDate: '2026-08-19',
        totalRevenue: 980,
        totalCost: 430,
        totalProfit: 550,
        totalOrdersCount: 8,
        deliveredOrdersCount: 5,
        cancelledOrdersCount: 0,
        deliveredItemsCount: 11,
        averageDeliveredOrderTotal: 196,
      },
      {
        startDate: '2026-08-20',
        endDate: '2026-08-20',
        totalRevenue: 1290,
        totalCost: 540,
        totalProfit: 750,
        totalOrdersCount: 9,
        deliveredOrdersCount: 6,
        cancelledOrdersCount: 1,
        deliveredItemsCount: 14,
        averageDeliveredOrderTotal: 215,
      },
    ],
  };
}
