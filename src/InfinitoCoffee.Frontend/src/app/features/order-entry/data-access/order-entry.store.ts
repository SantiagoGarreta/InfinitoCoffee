import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrderSource, type CreateOrderRequest } from '../../../core/orders/models/order.model';
import { ORDER_NUMBER_MAX_LENGTH } from '../../../core/orders/order.constants';
import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { ProductCategoriesApiService } from '../../../core/product-categories/data-access/product-categories-api.service';
import { ProductCategory } from '../../../core/product-categories/models/product-category.model';
import { ProductsApiService } from '../../../core/products/data-access/products-api.service';
import { Product } from '../../../core/products/models/product.model';

export interface EntryOrderItem {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  notes: string;
}

@Injectable({ providedIn: 'root' })
export class OrderEntryStore {
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly categories = signal<ProductCategory[]>([]);
  readonly products = signal<Product[]>([]);
  readonly selectedCategoryId = signal<string | null>(null);
  readonly orderNumber = signal('');
  readonly source = signal<OrderSource>('Counter');
  readonly notes = signal('');
  readonly items = signal<EntryOrderItem[]>([]);

  readonly activeCategories = computed(() => this.categories().filter((category) => category.isActive));
  readonly activeProducts = computed(() => this.products().filter((product) => product.isActive));
  readonly visibleProducts = computed(() => {
    const categoryId = this.selectedCategoryId();
    const products = this.activeProducts();

    if (!categoryId) {
      return products;
    }

    return products.filter((product) => product.categoryId === categoryId);
  });
  readonly visualTotal = computed(() =>
    this.items().reduce((sum, item) => sum + (item.unitPrice * item.quantity), 0),
  );
  readonly canSubmit = computed(() =>
    !this.submitting()
    && this.items().length > 0
    && this.orderNumber().trim().length > 0
    && this.orderNumber().trim().length <= ORDER_NUMBER_MAX_LENGTH,
  );

  private readonly ordersApiService = inject(OrdersApiService);
  private readonly productsApiService = inject(ProductsApiService);
  private readonly categoriesApiService = inject(ProductCategoriesApiService);

  async initialize(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      const [categories, products] = await Promise.all([
        this.categoriesApiService.getProductCategories(),
        this.productsApiService.getProducts(),
      ]);

      const activeCategories = categories.filter((category) => category.isActive);
      const activeProducts = products.filter((product) => product.isActive);

      this.categories.set(activeCategories);
      this.products.set(activeProducts);
      this.selectedCategoryId.set(activeCategories[0]?.id ?? null);
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar categorias y productos.'));
    } finally {
      this.loading.set(false);
    }
  }

  selectCategory(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
  }

  setOrderNumber(orderNumber: string): void {
    this.orderNumber.set(orderNumber);
  }

  setSource(source: OrderSource): void {
    this.source.set(source);
  }

  setNotes(notes: string): void {
    this.notes.set(notes);
  }

  addProduct(product: Product): void {
    this.successMessage.set(null);
    this.submitError.set(null);
    this.items.update((currentItems) => {
      const nextItems = [...currentItems];
      const existingItem = nextItems.find((item) => item.productId === product.id);

      if (existingItem) {
        existingItem.quantity += 1;
        return nextItems;
      }

      nextItems.push({
        productId: product.id,
        productName: product.name,
        unitPrice: product.price,
        quantity: 1,
        notes: '',
      });

      return nextItems;
    });
  }

  increaseQuantity(productId: string): void {
    this.items.update((currentItems) => currentItems.map((item) =>
      item.productId === productId
        ? { ...item, quantity: item.quantity + 1 }
        : item));
  }

  decreaseQuantity(productId: string): void {
    this.items.update((currentItems) => currentItems
      .map((item) => item.productId === productId
        ? { ...item, quantity: item.quantity - 1 }
        : item)
      .filter((item) => item.quantity > 0));
  }

  updateItemNotes(productId: string, notes: string): void {
    this.items.update((currentItems) => currentItems.map((item) =>
      item.productId === productId
        ? { ...item, notes }
        : item));
  }

  async submit(): Promise<void> {
    const request = this.buildRequest();
    if (!request) {
      this.submitError.set('Completa el numero de pedido y agrega al menos un producto.');
      return;
    }

    this.submitting.set(true);
    this.submitError.set(null);
    this.successMessage.set(null);

    try {
      const createdOrder = await this.ordersApiService.createOrder(request);
      this.successMessage.set(`Comanda creada correctamente. Pedido ${createdOrder.orderNumber}.`);
      this.resetForm();
    } catch (error: unknown) {
      this.submitError.set(toUserMessage(error, 'No fue posible crear la comanda.'));
    } finally {
      this.submitting.set(false);
    }
  }

  buildRequest(): CreateOrderRequest | null {
    const trimmedOrderNumber = this.orderNumber().trim();

    if (trimmedOrderNumber.length === 0 || this.items().length === 0 || trimmedOrderNumber.length > ORDER_NUMBER_MAX_LENGTH) {
      return null;
    }

    return {
      orderNumber: trimmedOrderNumber,
      source: this.source(),
      notes: normalizeOptionalString(this.notes()),
      items: this.items().map((item) => ({
        productId: item.productId,
        quantity: item.quantity,
        notes: normalizeOptionalString(item.notes),
      })),
    };
  }

  private resetForm(): void {
    this.orderNumber.set('');
    this.source.set('Counter');
    this.notes.set('');
    this.items.set([]);
  }
}

function normalizeOptionalString(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : null;
}
