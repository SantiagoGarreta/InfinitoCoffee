import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { OrderDto } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly ordersBaseUrl = `${environment.apiBaseUrl}/api/orders`;

  getActiveOrders(): Promise<OrderDto[]> {
    return firstValueFrom(this.httpClient.get<OrderDto[]>(`${this.ordersBaseUrl}/active`));
  }

  getPickupOrders(): Promise<OrderDto[]> {
    return firstValueFrom(this.httpClient.get<OrderDto[]>(`${this.ordersBaseUrl}/pickup`));
  }
}
