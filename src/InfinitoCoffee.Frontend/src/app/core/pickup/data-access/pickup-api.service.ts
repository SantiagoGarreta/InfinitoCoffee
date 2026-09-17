import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { PickupOrder } from '../models/pickup-order.model';

@Injectable({ providedIn: 'root' })
export class PickupApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly runtimeConfig = inject(APP_RUNTIME_CONFIG);

  getOrders(): Promise<PickupOrder[]> {
    return firstValueFrom(
      this.httpClient.get<PickupOrder[]>(`${this.runtimeConfig.apiBaseUrl}/api/orders/pickup`),
    );
  }
}
