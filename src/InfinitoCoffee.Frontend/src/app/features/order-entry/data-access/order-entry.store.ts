import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrderSource, type CreateOrderRequest } from '../../../core/orders/models/order.model';
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
  readonly source = signal<OrderSource>('Counter');
  readonly notes = signal('');
  readonly items = signal<EntryOrderItem[]>([]);

  readonly activeCategories = computed(() => this.categories().filter((category) => category.isActive));
  readonly visibleProducts = computed(() => {
    const activeCategoryIds = new Set(this.activeCategories().map((category) => category.id));
    const sellableProducts = this.products().filter((product) =>
      product.isActive && activeCategoryIds.has(product.categoryId));
    const categoryId = this.selectedCategoryId();

    if (!categoryId) {
      return sellableProducts;
    }

    return sellableProducts.filter((product) => product.categoryId === categoryId);
  });
  readonly visualTotal = computed(() =>
    this.items().reduce((sum, item) => sum + (item.unitPrice * item.quantity), 0),
  );
  readonly canSubmit = computed(() => !this.submitting() && this.items().length > 0);

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

      this.categories.set(categories);
      this.products.set(products);
      this.ensureSelectedCategory();
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar categorias y productos.'));
    } finally {
      this.loading.set(false);
    }
  }

  selectCategory(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
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
      this.submitError.set('Agrega al menos un producto para crear la comanda.');
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
    if (this.items().length === 0) {
      return null;
    }

    return {
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
    this.source.set('Counter');
    this.notes.set('');
    this.items.set([]);
  }

  private ensureSelectedCategory(): void {
    const activeCategories = this.activeCategories();
    const selectedCategoryId = this.selectedCategoryId();
    const selectedCategoryStillActive = activeCategories.some((category) => category.id === selectedCategoryId);

    if (!selectedCategoryStillActive) {
      this.selectedCategoryId.set(activeCategories[0]?.id ?? null);
    }
  }

}

function normalizeOptionalString(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : null;
}
