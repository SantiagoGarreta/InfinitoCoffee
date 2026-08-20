import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { OrderResults, OrderResultsGroupBy } from '../models/order-results.model';
import { CreateOrderRequest, OrderApiDto } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly ordersBaseUrl = `${this.appRuntimeConfig.apiBaseUrl}/api/orders`;

  getActiveOrders(): Promise<OrderApiDto[]> {
    return firstValueFrom(this.httpClient.get<OrderApiDto[]>(`${this.ordersBaseUrl}/active`));
  }

  getOrderSummary(groupBy: OrderResultsGroupBy = 'Daily'): Promise<OrderResults> {
    return firstValueFrom(
      this.httpClient.get<OrderResults>(`${this.ordersBaseUrl}/summary`, {
        params: { groupBy },
      }),
    );
  }

  createOrder(request: CreateOrderRequest): Promise<OrderApiDto> {
    return firstValueFrom(this.httpClient.post<OrderApiDto>(this.ordersBaseUrl, request));
  }

  startPreparation(orderId: string): Promise<OrderApiDto> {
    return firstValueFrom(this.httpClient.post<OrderApiDto>(`${this.ordersBaseUrl}/${orderId}/start-preparation`, {}));
  }

  markReady(orderId: string): Promise<OrderApiDto> {
    return firstValueFrom(this.httpClient.post<OrderApiDto>(`${this.ordersBaseUrl}/${orderId}/mark-ready`, {}));
  }

  deliver(orderId: string): Promise<OrderApiDto> {
    return firstValueFrom(this.httpClient.post<OrderApiDto>(`${this.ordersBaseUrl}/${orderId}/deliver`, {}));
  }

  cancel(orderId: string): Promise<OrderApiDto> {
    return firstValueFrom(this.httpClient.post<OrderApiDto>(`${this.ordersBaseUrl}/${orderId}/cancel`, {}));
  }
}
