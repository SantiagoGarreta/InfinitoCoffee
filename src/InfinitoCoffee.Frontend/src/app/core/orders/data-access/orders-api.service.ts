import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { CreateOrderRequest, Order } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly ordersBaseUrl = `${this.appRuntimeConfig.apiBaseUrl}/api/orders`;

  getActiveOrders(): Promise<Order[]> {
    return firstValueFrom(this.httpClient.get<Order[]>(`${this.ordersBaseUrl}/active`));
  }

  createOrder(request: CreateOrderRequest): Promise<Order> {
    return firstValueFrom(this.httpClient.post<Order>(this.ordersBaseUrl, request));
  }

  startPreparation(orderId: string): Promise<Order> {
    return firstValueFrom(this.httpClient.post<Order>(`${this.ordersBaseUrl}/${orderId}/start-preparation`, {}));
  }

  markReady(orderId: string): Promise<Order> {
    return firstValueFrom(this.httpClient.post<Order>(`${this.ordersBaseUrl}/${orderId}/mark-ready`, {}));
  }

  deliver(orderId: string): Promise<Order> {
    return firstValueFrom(this.httpClient.post<Order>(`${this.ordersBaseUrl}/${orderId}/deliver`, {}));
  }

  cancel(orderId: string): Promise<Order> {
    return firstValueFrom(this.httpClient.post<Order>(`${this.ordersBaseUrl}/${orderId}/cancel`, {}));
  }
}
