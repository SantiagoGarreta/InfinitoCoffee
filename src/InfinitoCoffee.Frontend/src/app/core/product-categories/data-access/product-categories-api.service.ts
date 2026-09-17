import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { ProductCategory } from '../models/product-category.model';

export interface SaveProductCategoryRequest {
  name: string;
}

@Injectable({ providedIn: 'root' })
export class ProductCategoriesApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly categoriesBaseUrl = `${this.appRuntimeConfig.apiBaseUrl}/api/product-categories`;

  getProductCategories(): Promise<ProductCategory[]> {
    return firstValueFrom(this.httpClient.get<ProductCategory[]>(this.categoriesBaseUrl));
  }

  createProductCategory(request: SaveProductCategoryRequest): Promise<ProductCategory> {
    return firstValueFrom(this.httpClient.post<ProductCategory>(this.categoriesBaseUrl, request));
  }

  updateProductCategory(categoryId: string, request: SaveProductCategoryRequest): Promise<ProductCategory> {
    return firstValueFrom(this.httpClient.put<ProductCategory>(`${this.categoriesBaseUrl}/${categoryId}`, request));
  }

  activateProductCategory(categoryId: string): Promise<ProductCategory> {
    return firstValueFrom(this.httpClient.post<ProductCategory>(`${this.categoriesBaseUrl}/${categoryId}/activate`, null));
  }

  deactivateProductCategory(categoryId: string): Promise<ProductCategory> {
    return firstValueFrom(this.httpClient.post<ProductCategory>(`${this.categoriesBaseUrl}/${categoryId}/deactivate`, null));
  }
}
