import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { ProductCategory } from '../models/product-category.model';

@Injectable({ providedIn: 'root' })
export class ProductCategoriesApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly appRuntimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly categoriesBaseUrl = `${this.appRuntimeConfig.apiBaseUrl}/api/product-categories`;

  getProductCategories(): Promise<ProductCategory[]> {
    return firstValueFrom(this.httpClient.get<ProductCategory[]>(this.categoriesBaseUrl));
  }
}
