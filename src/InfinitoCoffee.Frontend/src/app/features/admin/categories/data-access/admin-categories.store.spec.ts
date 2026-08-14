import { TestBed } from '@angular/core/testing';

import { ProductCategoriesApiService, SaveProductCategoryRequest } from '../../../../core/product-categories/data-access/product-categories-api.service';
import { ProductCategory } from '../../../../core/product-categories/models/product-category.model';
import { AdminCategoriesStore } from './admin-categories.store';

class FakeCategoriesApiService {
  categories: ProductCategory[] = [
    { id: 'c-2', name: 'Tés', isActive: false },
    { id: 'c-1', name: 'Cafés', isActive: true },
  ];
  error: unknown = null;
  getProductCategories(): Promise<ProductCategory[]> { return this.result(this.categories); }
  createProductCategory(request: SaveProductCategoryRequest): Promise<ProductCategory> {
    return this.result({ id: 'c-3', ...request, isActive: true });
  }
  updateProductCategory(id: string, request: SaveProductCategoryRequest): Promise<ProductCategory> {
    return this.result({ id, ...request, isActive: true });
  }
  activateProductCategory(id: string): Promise<ProductCategory> {
    return this.result({ ...this.categories.find((category) => category.id === id)!, isActive: true });
  }
  deactivateProductCategory(id: string): Promise<ProductCategory> {
    return this.result({ ...this.categories.find((category) => category.id === id)!, isActive: false });
  }
  private result<T>(value: T): Promise<T> { return this.error ? Promise.reject(this.error) : Promise.resolve(value); }
}

describe('AdminCategoriesStore', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [
      AdminCategoriesStore,
      { provide: ProductCategoriesApiService, useClass: FakeCategoriesApiService },
    ],
  }));

  it('loads and sorts active and inactive categories', async () => {
    const store = TestBed.inject(AdminCategoriesStore);
    await store.load();
    expect(store.sortedCategories().map((category) => category.name)).toEqual(['Cafés', 'Tés']);
  });

  it('creates, updates, activates and deactivates from backend responses', async () => {
    const store = TestBed.inject(AdminCategoriesStore);
    await store.load();
    await store.create({ name: 'Fríos' });
    await store.update('c-3', { name: 'Bebidas frías' });
    await store.activate('c-2');
    await store.deactivate('c-1');
    expect(store.categories().find((category) => category.id === 'c-3')?.name).toBe('Bebidas frías');
    expect(store.categories().find((category) => category.id === 'c-2')?.isActive).toBe(true);
    expect(store.categories().find((category) => category.id === 'c-1')?.isActive).toBe(false);
  });

  it('exposes load and mutation errors', async () => {
    const store = TestBed.inject(AdminCategoriesStore);
    const api = TestBed.inject(ProductCategoriesApiService) as unknown as FakeCategoriesApiService;
    api.error = new Error('failed');
    await store.load();
    expect(store.loadError()).not.toBeNull();
    await store.create({ name: 'Fríos' });
    expect(store.mutationError()).not.toBeNull();
  });
});
