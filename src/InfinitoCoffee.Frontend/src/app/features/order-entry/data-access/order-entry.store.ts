import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../core/http/api-error.utils';
import { OrderSource, type CreateOrderRequest } from '../../../core/orders/models/order.model';
import { OrdersApiService } from '../../../core/orders/data-access/orders-api.service';
import { ProductCategoriesApiService } from '../../../core/product-categories/data-access/product-categories-api.service';
import { ProductCategory } from '../../../core/product-categories/models/product-category.model';
import { ProductsApiService, SaveProductRequest } from '../../../core/products/data-access/products-api.service';
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
  readonly menuSaving = signal(false);
  readonly menuError = signal<string | null>(null);
  readonly menuSuccessMessage = signal<string | null>(null);
  readonly categories = signal<ProductCategory[]>([]);
  readonly products = signal<Product[]>([]);
  readonly selectedCategoryId = signal<string | null>(null);
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

  clearMenuFeedback(): void {
    this.menuError.set(null);
    this.menuSuccessMessage.set(null);
  }

  async createMenuProduct(request: SaveProductRequest): Promise<void> {
    await this.runMenuMutation(
      async () => this.productsApiService.createProduct(request),
      (product) => {
        this.upsertProduct(product);
        this.menuSuccessMessage.set(`Producto "${product.name}" agregado al menu.`);
      },
      'No fue posible agregar el producto.',
    );
  }

  async updateMenuProduct(productId: string, request: SaveProductRequest): Promise<void> {
    await this.runMenuMutation(
      async () => this.productsApiService.updateProduct(productId, request),
      (product) => {
        this.upsertProduct(product);
        this.items.update((currentItems) => currentItems.map((item) =>
          item.productId === product.id
            ? { ...item, productName: product.name, unitPrice: product.price }
            : item));
        this.menuSuccessMessage.set(`Producto "${product.name}" actualizado.`);
      },
      'No fue posible actualizar el producto.',
    );
  }

  async deleteMenuProduct(productId: string): Promise<void> {
    const existingProduct = this.products().find((product) => product.id === productId);

    this.menuSaving.set(true);
    this.menuError.set(null);
    this.menuSuccessMessage.set(null);

    try {
      await this.productsApiService.deleteProduct(productId);
      this.products.update((currentProducts) => currentProducts.filter((product) => product.id !== productId));
      this.items.update((currentItems) => currentItems.filter((item) => item.productId !== productId));
      this.menuSuccessMessage.set(`Producto "${existingProduct?.name ?? 'seleccionado'}" eliminado del menu.`);
      this.ensureSelectedCategory();
    } catch (error: unknown) {
      this.menuError.set(toUserMessage(error, 'No fue posible eliminar el producto.'));
    } finally {
      this.menuSaving.set(false);
    }
  }

  private resetForm(): void {
    this.source.set('Counter');
    this.notes.set('');
    this.items.set([]);
  }

  private async runMenuMutation(
    action: () => Promise<Product>,
    onSuccess: (product: Product) => void,
    fallbackMessage: string,
  ): Promise<void> {
    this.menuSaving.set(true);
    this.menuError.set(null);
    this.menuSuccessMessage.set(null);

    try {
      const product = await action();
      onSuccess(product);
      this.ensureSelectedCategory();
    } catch (error: unknown) {
      this.menuError.set(toUserMessage(error, fallbackMessage));
    } finally {
      this.menuSaving.set(false);
    }
  }

  private ensureSelectedCategory(): void {
    const activeCategories = this.activeCategories();
    const selectedCategoryId = this.selectedCategoryId();
    const selectedCategoryStillActive = activeCategories.some((category) => category.id === selectedCategoryId);

    if (!selectedCategoryStillActive) {
      this.selectedCategoryId.set(activeCategories[0]?.id ?? null);
    }
  }

  private upsertProduct(product: Product): void {
    this.products.update((currentProducts) => {
      const nextProducts = currentProducts.filter((currentProduct) => currentProduct.id !== product.id);
      nextProducts.push(product);
      return nextProducts.sort((left, right) => left.name.localeCompare(right.name, 'es'));
    });
  }
}

function normalizeOptionalString(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : null;
}
