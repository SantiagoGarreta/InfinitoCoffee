import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { Product } from '../models/product.model';

@Injectable({ providedIn: 'root' })
export class ProductsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly productsBaseUrl = `${environment.apiBaseUrl}/api/products`;

  getProducts(): Promise<Product[]> {
    return firstValueFrom(this.httpClient.get<Product[]>(this.productsBaseUrl));
  }
}
