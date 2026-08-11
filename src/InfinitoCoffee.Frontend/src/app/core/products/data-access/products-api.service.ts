import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { Product } from '../models/product.model';

export interface SaveProductRequest {
  name: string;
  description: string | null;
  price: number;
  categoryId: string;
}

@Injectable({ providedIn: 'root' })
export class ProductsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly productsBaseUrl = `${this.appRuntimeConfig.apiBaseUrl}/api/products`;

  getProducts(): Promise<Product[]> {
    return firstValueFrom(this.httpClient.get<Product[]>(this.productsBaseUrl));
  }

  createProduct(request: SaveProductRequest): Promise<Product> {
    return firstValueFrom(this.httpClient.post<Product>(this.productsBaseUrl, request));
  }

  updateProduct(productId: string, request: SaveProductRequest): Promise<Product> {
    return firstValueFrom(this.httpClient.put<Product>(`${this.productsBaseUrl}/${productId}`, request));
  }

  deleteProduct(productId: string): Promise<void> {
    return firstValueFrom(
      this.httpClient.post<void>(`${this.productsBaseUrl}/${productId}/deactivate`, { hardDelete: true }),
    );
  }
}
