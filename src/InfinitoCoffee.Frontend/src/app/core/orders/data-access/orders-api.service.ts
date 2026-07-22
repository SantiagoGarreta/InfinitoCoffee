import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { CreateOrderRequest, Order } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly ordersBaseUrl = `${environment.apiBaseUrl}/api/orders`;

  getActiveOrders(): Promise<Order[]> {
    return firstValueFrom(this.httpClient.get<Order[]>(`${this.ordersBaseUrl}/active`));
  }

  getPickupOrders(): Promise<Order[]> {
    return firstValueFrom(this.httpClient.get<Order[]>(`${this.ordersBaseUrl}/pickup`));
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
