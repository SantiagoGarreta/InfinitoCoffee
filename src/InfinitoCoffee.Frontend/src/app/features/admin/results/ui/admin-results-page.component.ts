import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';

import { OrdersApiService } from '../../../../core/orders/data-access/orders-api.service';
import {
  OrderHistoryPoint,
  OrderResults,
  OrderResultsGroupBy,
} from '../../../../core/orders/models/order-results.model';
import { ErrorMessageComponent } from '../../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../../shared/ui/loading-state/loading-state.component';

type SummaryCardKind = 'currency' | 'number';

type SummaryCard = {
  label: string;
  value: number;
  kind: SummaryCardKind;
};

type ComparisonCard = {
  label: string;
  currentValue: number;
  previousValue: number;
  currentHeight: number;
  previousHeight: number;
  delta: number;
  kind: SummaryCardKind;
};

type TrendChartPoint = {
  id: string;
  label: string;
  x: number;
  revenueY: number;
  profitY: number;
  revenueValue: number;
  profitValue: number;
};

type TrendChartGridLine = {
  label: string;
  y: number;
};

type TrendChartModel = {
  width: number;
  height: number;
  revenuePolyline: string;
  profitPolyline: string;
  points: TrendChartPoint[];
  gridLines: TrendChartGridLine[];
};

type ColumnChartItem = {
  id: string;
  label: string;
  value: number;
  height: number;
  detail: string;
  tone: 'coffee' | 'sage';
};

@Component({
  selector: 'app-admin-results-page',
  standalone: true,
  imports: [CommonModule, ErrorMessageComponent, LoadingStateComponent],
  templateUrl: './admin-results-page.component.html',
  styleUrl: './admin-results-page.component.scss',
})
export class AdminResultsPageComponent implements OnInit {
  private readonly ordersApi = inject(OrdersApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly periodLabelFormatter = new Intl.DateTimeFormat('es-UY', {
    day: 'numeric',
    month: 'short',
  });
  private readonly monthFormatter = new Intl.DateTimeFormat('es-UY', {
    month: 'long',
    year: 'numeric',
  });

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly results = signal<OrderResults | null>(null);
  readonly selectedGroupBy = signal<OrderResultsGroupBy>('Daily');
  readonly groupOptions: ReadonlyArray<{ value: OrderResultsGroupBy; label: string }> = [
    { value: 'Daily', label: 'Diario' },
    { value: 'Weekly', label: 'Semanal' },
    { value: 'Monthly', label: 'Mensual' },
  ];
  readonly summaryCards = computed<SummaryCard[]>(() => {
    const currentPeriod = this.results()?.currentPeriod;
    if (!currentPeriod) {
      return [];
    }

    return [
      { label: 'Ingresos del periodo', value: currentPeriod.totalRevenue, kind: 'currency' },
      { label: 'Costos del periodo', value: currentPeriod.totalCost, kind: 'currency' },
      { label: 'Ganancia del periodo', value: currentPeriod.totalProfit, kind: 'currency' },
      { label: 'Pedidos entregados', value: currentPeriod.deliveredOrdersCount, kind: 'number' },
      { label: 'Productos vendidos', value: currentPeriod.deliveredItemsCount, kind: 'number' },
      { label: 'Ticket promedio', value: currentPeriod.averageDeliveredOrderTotal, kind: 'currency' },
    ];
  });
  readonly comparisonCards = computed<ComparisonCard[]>(() => {
    const results = this.results();
    if (!results) {
      return [];
    }

    const current = results.currentPeriod;
    const previous = results.previousPeriod;

    const metrics = [
      { label: 'Ingresos', currentValue: current.totalRevenue, previousValue: previous.totalRevenue, kind: 'currency' as const },
      { label: 'Ganancia', currentValue: current.totalProfit, previousValue: previous.totalProfit, kind: 'currency' as const },
      {
        label: 'Pedidos entregados',
        currentValue: current.deliveredOrdersCount,
        previousValue: previous.deliveredOrdersCount,
        kind: 'number' as const,
      },
    ];
    const maxValue = Math.max(1, ...metrics.flatMap(metric => [metric.currentValue, metric.previousValue]));

    return metrics.map(metric =>
      this.createComparisonCard(
        metric.label,
        metric.currentValue,
        metric.previousValue,
        (metric.currentValue / maxValue) * 100,
        (metric.previousValue / maxValue) * 100,
        metric.kind,
      ),
    );
  });
  readonly operationalStats = computed(() => {
    const snapshot = this.results()?.operationalSnapshot;
    if (!snapshot) {
      return [];
    }

    return [
      { label: 'Pedidos totales', value: snapshot.totalOrdersCount },
      { label: 'Pedidos activos', value: snapshot.activeOrdersCount },
      { label: 'Pendientes', value: snapshot.pendingOrdersCount },
      { label: 'En preparacion', value: snapshot.preparingOrdersCount },
      { label: 'Listos para retiro', value: snapshot.readyOrdersCount },
      { label: 'Cancelados', value: snapshot.cancelledOrdersCount },
    ];
  });
  readonly trendChart = computed<TrendChartModel | null>(() => {
    const history = this.results()?.history ?? [];
    if (history.length === 0) {
      return null;
    }

    const width = 720;
    const height = 320;
    const paddingLeft = 56;
    const paddingRight = 20;
    const paddingTop = 18;
    const paddingBottom = 46;
    const plotWidth = width - paddingLeft - paddingRight;
    const plotHeight = height - paddingTop - paddingBottom;
    const maxValue = Math.max(1, ...history.flatMap(point => [point.totalRevenue, Math.max(point.totalProfit, 0)]));
    const toY = (value: number) => paddingTop + plotHeight - (Math.max(value, 0) / maxValue) * plotHeight;

    const points = history.map((point, index) => {
      const x =
        history.length === 1
          ? paddingLeft + plotWidth / 2
          : paddingLeft + (plotWidth * index) / (history.length - 1);

      return {
        id: `${point.startDate}-${point.endDate}`,
        label: this.formatPeriodRange(point.startDate, point.endDate, this.selectedGroupBy()),
        x,
        revenueY: toY(point.totalRevenue),
        profitY: toY(point.totalProfit),
        revenueValue: point.totalRevenue,
        profitValue: point.totalProfit,
      };
    });

    const gridLines = Array.from({ length: 4 }, (_, index) => {
      const ratio = index / 3;
      const value = maxValue * (1 - ratio);

      return {
        label: this.formatCompactCurrency(value),
        y: paddingTop + plotHeight * ratio,
      };
    });

    return {
      width,
      height,
      revenuePolyline: points.map(point => `${point.x},${point.revenueY}`).join(' '),
      profitPolyline: points.map(point => `${point.x},${point.profitY}`).join(' '),
      points,
      gridLines,
    };
  });
  readonly productChart = computed<ColumnChartItem[]>(() => {
    const products = (this.results()?.topSellingProducts ?? []).slice(0, 6);
    const maxValue = Math.max(1, ...products.map(product => product.quantitySold));

    return products.map(product => ({
      id: product.productId,
      label: product.productName,
      value: product.quantitySold,
      height: (product.quantitySold / maxValue) * 100,
      detail: this.formatCompactCurrency(product.revenue),
      tone: 'sage',
    }));
  });
  readonly selectedPeriodLabel = computed(() => {
    const results = this.results();
    if (!results) {
      return '';
    }

    return this.formatPeriodRange(results.periodStartDate, results.periodEndDate, results.groupBy);
  });
  readonly previousPeriodLabel = computed(() => {
    const previousPeriod = this.results()?.previousPeriod;
    return previousPeriod
      ? this.formatPeriodRange(previousPeriod.startDate, previousPeriod.endDate, this.selectedGroupBy())
      : '';
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      this.loading.set(false);
      return;
    }

    void this.reload();
  }

  async reload(groupBy = this.selectedGroupBy()): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      const results = await this.ordersApi.getOrderSummary(groupBy);
      this.selectedGroupBy.set(results.groupBy);
      this.results.set(results);
    } catch {
      this.loadError.set('No fue posible cargar el resultado de ventas. Intenta nuevamente.');
    } finally {
      this.loading.set(false);
    }
  }

  async changeGroupBy(groupBy: OrderResultsGroupBy): Promise<void> {
    if (groupBy === this.selectedGroupBy()) {
      return;
    }

    this.selectedGroupBy.set(groupBy);
    await this.reload(groupBy);
  }

  formatPeriodRange(startDate: string, endDate: string, groupBy: OrderResultsGroupBy): string {
    const start = this.parseDate(startDate);
    const end = this.parseDate(endDate);

    if (groupBy === 'Monthly') {
      return this.capitalize(this.monthFormatter.format(start));
    }

    if (startDate === endDate) {
      return this.capitalize(this.periodLabelFormatter.format(start));
    }

    return `${this.capitalize(this.periodLabelFormatter.format(start))} - ${this.capitalize(this.periodLabelFormatter.format(end))}`;
  }

  formatDelta(delta: number, kind: SummaryCardKind): string {
    const absoluteDelta = Math.abs(delta);

    if (kind === 'currency') {
      return this.formatCurrencyValue(absoluteDelta);
    }

    return absoluteDelta.toString();
  }

  formatCurrencyValue(value: number): string {
    return new Intl.NumberFormat('es-UY', {
      style: 'currency',
      currency: 'UYU',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  formatCompactCurrency(value: number): string {
    return new Intl.NumberFormat('es-UY', {
      style: 'currency',
      currency: 'UYU',
      notation: value >= 1000 ? 'compact' : 'standard',
      maximumFractionDigits: value >= 1000 ? 1 : 0,
    }).format(value);
  }

  trackTrendPoint(_: number, point: TrendChartPoint): string {
    return point.id;
  }

  trackChartItem(_: number, item: ColumnChartItem): string {
    return item.id;
  }

  private createComparisonCard(
    label: string,
    currentValue: number,
    previousValue: number,
    currentHeight: number,
    previousHeight: number,
    kind: SummaryCardKind,
  ): ComparisonCard {
    return {
      label,
      currentValue,
      previousValue,
      currentHeight,
      previousHeight,
      delta: currentValue - previousValue,
      kind,
    };
  }

  private parseDate(date: string): Date {
    return new Date(`${date}T00:00:00`);
  }

  private capitalize(value: string): string {
    if (value.length === 0) {
      return value;
    }

    return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
  }
}
