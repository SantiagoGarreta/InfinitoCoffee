import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';

import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { ProductCategoriesApiService } from '../../../core/product-categories/data-access/product-categories-api.service';
import { ProductsApiService } from '../../../core/products/data-access/products-api.service';
import { OrderEntryStore } from './order-entry.store';

class FakeOrdersApiService {
  lastCreateRequest: unknown;
  createOrderError: unknown = null;

  createOrder(request: unknown): Promise<{ orderNumber: string }> {
    this.lastCreateRequest = request;

    if (this.createOrderError) {
      return Promise.reject(this.createOrderError);
    }

    return Promise.resolve({ orderNumber: 'A-101' });
  }
}

class FakeProductsApiService {
  getProducts(): Promise<Array<{ id: string; name: string; description: string | null; price: number; categoryId: string; isActive: boolean }>> {
    return Promise.resolve([
      { id: 'p-1', name: 'Espresso', description: null, price: 8, categoryId: 'c-1', isActive: true },
      { id: 'p-2', name: 'Mocha', description: null, price: 10, categoryId: 'c-2', isActive: false },
    ]);
  }
}

class FakeProductCategoriesApiService {
  getProductCategories(): Promise<Array<{ id: string; name: string; isActive: boolean }>> {
    return Promise.resolve([
      { id: 'c-1', name: 'Cafe', isActive: true },
      { id: 'c-2', name: 'Temporales', isActive: false },
    ]);
  }
}

describe('OrderEntryStore', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        OrderEntryStore,
        { provide: OrdersApiService, useClass: FakeOrdersApiService },
        { provide: ProductsApiService, useClass: FakeProductsApiService },
        { provide: ProductCategoriesApiService, useClass: FakeProductCategoriesApiService },
      ],
    });
  });

  it('loads only active categories and products', async () => {
    const store = TestBed.inject(OrderEntryStore);

    await store.initialize();

    expect(store.activeCategories().map((category) => category.name)).toEqual(['Cafe']);
    expect(store.visibleProducts().map((product) => product.name)).toEqual(['Espresso']);
  });

  it('adds a product, increments repeated items and decreases quantity', async () => {
    const store = TestBed.inject(OrderEntryStore);
    await store.initialize();

    const product = store.visibleProducts()[0]!;
    store.addProduct(product);
    store.addProduct(product);
    store.decreaseQuantity(product.id);

    expect(store.items()[0]?.quantity).toBe(1);
  });

  it('calculates the visual total and builds the correct request', async () => {
    const store = TestBed.inject(OrderEntryStore);
    await store.initialize();

    const product = store.visibleProducts()[0]!;
    store.setOrderNumber('  A-101  ');
    store.setNotes(' Mesa 2 ');
    store.addProduct(product);
    store.updateItemNotes(product.id, ' Sin canela ');

    expect(store.visualTotal()).toBe(8);
    expect(store.buildRequest()).toEqual({
      orderNumber: 'A-101',
      source: 'Counter',
      notes: 'Mesa 2',
      items: [
        {
          productId: 'p-1',
          quantity: 1,
          notes: 'Sin canela',
        },
      ],
    });
  });

  it('cleans the form after creating an order', async () => {
    const store = TestBed.inject(OrderEntryStore);
    await store.initialize();

    const product = store.visibleProducts()[0]!;
    store.setOrderNumber('A-101');
    store.addProduct(product);

    await store.submit();

    expect(store.orderNumber()).toBe('');
    expect(store.items()).toEqual([]);
    expect(store.source()).toBe('Counter');
    expect(store.successMessage()).toContain('A-101');
  });

  it('shows a friendly duplicate order number error', async () => {
    const store = TestBed.inject(OrderEntryStore);
    const ordersApi = TestBed.inject(OrdersApiService) as unknown as FakeOrdersApiService;
    await store.initialize();

    const product = store.visibleProducts()[0]!;
    store.setOrderNumber('A-101');
    store.addProduct(product);
    ordersApi.createOrderError = new HttpErrorResponse({
      status: 409,
      error: {
        detail: 'El numero de pedido ya existe.',
      },
    });

    await store.submit();

    expect(store.submitError()).toBe('El numero de pedido ya existe.');
  });
});
