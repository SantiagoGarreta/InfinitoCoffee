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
  createdProducts: Array<{ id: string; name: string; description: string | null; price: number; categoryId: string; isActive: boolean }> = [];
  updatedProducts: Array<{ id: string; name: string; description: string | null; price: number; categoryId: string; isActive: boolean }> = [];
  deletedProductIds: string[] = [];

  getProducts(): Promise<Array<{ id: string; name: string; description: string | null; price: number; categoryId: string; isActive: boolean }>> {
    return Promise.resolve([
      { id: 'p-1', name: 'Espresso', description: null, price: 8, categoryId: 'c-1', isActive: true },
      { id: 'p-2', name: 'Mocha', description: null, price: 10, categoryId: 'c-2', isActive: false },
    ]);
  }

  createProduct(request: { name: string; description: string | null; price: number; categoryId: string }): Promise<{ id: string; name: string; description: string | null; price: number; categoryId: string; isActive: boolean }> {
    const product = { id: 'p-3', ...request, isActive: true };
    this.createdProducts.push(product);
    return Promise.resolve(product);
  }

  updateProduct(productId: string, request: { name: string; description: string | null; price: number; categoryId: string }): Promise<{ id: string; name: string; description: string | null; price: number; categoryId: string; isActive: boolean }> {
    const product = { id: productId, ...request, isActive: true };
    this.updatedProducts.push(product);
    return Promise.resolve(product);
  }

  deleteProduct(productId: string): Promise<void> {
    this.deletedProductIds.push(productId);
    return Promise.resolve();
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

  it('creates a menu product and makes it available in the visible catalog', async () => {
    const store = TestBed.inject(OrderEntryStore);
    const productsApi = TestBed.inject(ProductsApiService) as unknown as FakeProductsApiService;
    await store.initialize();

    await store.createMenuProduct({
      name: 'Flat White',
      description: 'Doble shot',
      price: 12,
      categoryId: 'c-1',
    });

    expect(productsApi.createdProducts[0]?.name).toBe('Flat White');
    expect(store.visibleProducts().map((product) => product.name)).toEqual(['Espresso', 'Flat White']);
    expect(store.menuSuccessMessage()).toContain('Flat White');
  });

  it('updates a menu product and synchronizes any matching order item snapshot', async () => {
    const store = TestBed.inject(OrderEntryStore);
    await store.initialize();

    const product = store.visibleProducts()[0]!;
    store.addProduct(product);

    await store.updateMenuProduct(product.id, {
      name: 'Espresso Largo',
      description: 'Mas agua',
      price: 9,
      categoryId: 'c-1',
    });

    expect(store.visibleProducts()[0]?.name).toBe('Espresso Largo');
    expect(store.items()[0]?.productName).toBe('Espresso Largo');
    expect(store.items()[0]?.unitPrice).toBe(9);
  });

  it('deletes a menu product and removes it from the current order', async () => {
    const store = TestBed.inject(OrderEntryStore);
    const productsApi = TestBed.inject(ProductsApiService) as unknown as FakeProductsApiService;
    await store.initialize();

    const product = store.visibleProducts()[0]!;
    store.addProduct(product);

    await store.deleteMenuProduct(product.id);

    expect(productsApi.deletedProductIds).toEqual([product.id]);
    expect(store.visibleProducts()).toEqual([]);
    expect(store.items()).toEqual([]);
  });
});
