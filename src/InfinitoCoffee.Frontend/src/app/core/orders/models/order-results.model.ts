export type OrderResultsGroupBy = 'Daily' | 'Weekly' | 'Monthly';

export interface TopSellingProduct {
  productId: string;
  productName: string;
  quantitySold: number;
  revenue: number;
}

export interface OrderPeriodSummary {
  startDate: string;
  endDate: string;
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  totalOrdersCount: number;
  deliveredOrdersCount: number;
  cancelledOrdersCount: number;
  deliveredItemsCount: number;
  averageDeliveredOrderTotal: number;
}

export interface OrderOperationalSnapshot {
  totalOrdersCount: number;
  activeOrdersCount: number;
  pendingOrdersCount: number;
  preparingOrdersCount: number;
  readyOrdersCount: number;
  deliveredOrdersCount: number;
  cancelledOrdersCount: number;
}

export interface OrderHistoryPoint extends OrderPeriodSummary {}

export interface OrderResults {
  groupBy: OrderResultsGroupBy;
  periodStartDate: string;
  periodEndDate: string;
  currentPeriod: OrderPeriodSummary;
  previousPeriod: OrderPeriodSummary;
  operationalSnapshot: OrderOperationalSnapshot;
  topSellingProducts: TopSellingProduct[];
  history: OrderHistoryPoint[];
}
