import { TestBed } from '@angular/core/testing';

import { ProductCategoriesApiService } from '../../../../core/product-categories/data-access/product-categories-api.service';
import { ProductsApiService, SaveProductRequest } from '../../../../core/products/data-access/products-api.service';
import { Product } from '../../../../core/products/models/product.model';
import { AdminProductsStore } from './admin-products.store';

class FakeProductsApiService {
  products: Product[] = [
    { id: 'p-2', name: 'Latte', description: null, price: 10, cost: 4, categoryId: 'c-2', isActive: true },
    { id: 'p-1', name: 'Espresso', description: null, price: 8, cost: 3, categoryId: 'c-1', isActive: false },
  ];
  error: unknown = null;

  getProducts(): Promise<Product[]> { return this.result(this.products); }
  createProduct(request: SaveProductRequest): Promise<Product> {
    return this.result({ id: 'p-3', ...request, isActive: true });
  }
  updateProduct(id: string, request: SaveProductRequest): Promise<Product> {
    return this.result({ id, ...request, isActive: true });
  }
  activateProduct(id: string): Promise<Product> {
    return this.result({ ...this.products.find((product) => product.id === id)!, isActive: true });
  }
  deactivateProduct(id: string): Promise<Product> {
    return this.result({ ...this.products.find((product) => product.id === id)!, isActive: false });
  }
  private result<T>(value: T): Promise<T> { return this.error ? Promise.reject(this.error) : Promise.resolve(value); }
}

class FakeCategoriesApiService {
  getProductCategories() {
    return Promise.resolve([
      { id: 'c-2', name: 'Tés', isActive: false },
      { id: 'c-1', name: 'Cafés', isActive: true },
    ]);
  }
}

describe('AdminProductsStore', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [
      AdminProductsStore,
      { provide: ProductsApiService, useClass: FakeProductsApiService },
      { provide: ProductCategoriesApiService, useClass: FakeCategoriesApiService },
    ],
  }));

  it('loads every product and sorts by category then product name', async () => {
    const store = TestBed.inject(AdminProductsStore);
    await store.load();
    expect(store.sortedProducts().map((product) => product.name)).toEqual(['Espresso', 'Latte']);
    expect(store.products().map((product) => product.isActive)).toEqual([true, false]);
  });

  it('creates and updates products from backend responses', async () => {
    const store = TestBed.inject(AdminProductsStore);
    await store.load();
    await store.create({ name: 'Mocha', description: null, price: 12, cost: 5, categoryId: 'c-1' });
    await store.update('p-3', { name: 'Mocaccino', description: null, price: 13, cost: 5.5, categoryId: 'c-1' });
    expect(store.products().find((product) => product.id === 'p-3')?.name).toBe('Mocaccino');
    expect(store.products().find((product) => product.id === 'p-3')?.cost).toBe(5.5);
    expect(store.successMessage()).toContain('actualizado');
  });

  it('activates and deactivates without optimistic changes', async () => {
    const store = TestBed.inject(AdminProductsStore);
    await store.load();
    await store.activate('p-1');
    expect(store.products().find((product) => product.id === 'p-1')?.isActive).toBe(true);
    await store.deactivate('p-2');
    expect(store.products().find((product) => product.id === 'p-2')?.isActive).toBe(false);
  });

  it('exposes load and mutation errors', async () => {
    const store = TestBed.inject(AdminProductsStore);
    const api = TestBed.inject(ProductsApiService) as unknown as FakeProductsApiService;
    api.error = new Error('failed');
    await store.load();
    expect(store.loadError()).not.toBeNull();
    await store.create({ name: 'Mocha', description: null, price: 12, cost: 5, categoryId: 'c-1' });
    expect(store.mutationError()).not.toBeNull();
  });
});
