import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { ProductCategory } from '../models/product-category.model';

@Injectable({ providedIn: 'root' })
export class ProductCategoriesApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly categoriesBaseUrl = `${environment.apiBaseUrl}/api/product-categories`;

  getProductCategories(): Promise<ProductCategory[]> {
    return firstValueFrom(this.httpClient.get<ProductCategory[]>(this.categoriesBaseUrl));
  }
}
