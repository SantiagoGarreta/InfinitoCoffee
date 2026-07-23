import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { Product } from '../models/product.model';

@Injectable({ providedIn: 'root' })
export class ProductsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly productsBaseUrl = `${this.appRuntimeConfig.apiBaseUrl}/api/products`;

  getProducts(): Promise<Product[]> {
    return firstValueFrom(this.httpClient.get<Product[]>(this.productsBaseUrl));
  }
}
