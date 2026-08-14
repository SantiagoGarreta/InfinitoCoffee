import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../../core/http/api-error.utils';
import { ProductCategoriesApiService } from '../../../../core/product-categories/data-access/product-categories-api.service';
import { ProductCategory } from '../../../../core/product-categories/models/product-category.model';
import { ProductsApiService, SaveProductRequest } from '../../../../core/products/data-access/products-api.service';
import { Product } from '../../../../core/products/models/product.model';

@Injectable({ providedIn: 'root' })
export class AdminProductsStore {
  readonly products = signal<Product[]>([]);
  readonly categories = signal<ProductCategory[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly mutationError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly activeCategories = computed(() => this.categories()
    .filter((category) => category.isActive)
    .sort(compareCategories));

  readonly sortedProducts = computed(() => {
    const categoryNames = new Map(this.categories().map((category) => [category.id, category.name]));
    return [...this.products()].sort((left, right) => {
      const categoryComparison = (categoryNames.get(left.categoryId) ?? '')
        .localeCompare(categoryNames.get(right.categoryId) ?? '', 'es');
      return categoryComparison
        || left.name.localeCompare(right.name, 'es')
        || left.id.localeCompare(right.id);
    });
  });

  private readonly productsApiService = inject(ProductsApiService);
  private readonly categoriesApiService = inject(ProductCategoriesApiService);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      const [products, categories] = await Promise.all([
        this.productsApiService.getProducts(),
        this.categoriesApiService.getProductCategories(),
      ]);
      this.products.set(products);
      this.categories.set(categories);
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar productos y categorías.'));
    } finally {
      this.loading.set(false);
    }
  }

  create(request: SaveProductRequest): Promise<Product | null> {
    return this.runMutation(
      () => this.productsApiService.createProduct(request),
      (product) => `Producto "${product.name}" creado.`,
      'No fue posible crear el producto.',
    );
  }

  update(productId: string, request: SaveProductRequest): Promise<Product | null> {
    return this.runMutation(
      () => this.productsApiService.updateProduct(productId, request),
      (product) => `Producto "${product.name}" actualizado.`,
      'No fue posible actualizar el producto.',
    );
  }

  activate(productId: string): Promise<Product | null> {
    return this.runMutation(
      () => this.productsApiService.activateProduct(productId),
      (product) => `Producto "${product.name}" activado.`,
      'No fue posible activar el producto.',
    );
  }

  deactivate(productId: string): Promise<Product | null> {
    return this.runMutation(
      () => this.productsApiService.deactivateProduct(productId),
      (product) => `Producto "${product.name}" desactivado.`,
      'No fue posible desactivar el producto.',
    );
  }

  clearFeedback(): void {
    this.mutationError.set(null);
    this.successMessage.set(null);
  }

  categoryFor(categoryId: string): ProductCategory | undefined {
    return this.categories().find((category) => category.id === categoryId);
  }

  private async runMutation(
    action: () => Promise<Product>,
    successMessage: (product: Product) => string,
    fallbackMessage: string,
  ): Promise<Product | null> {
    this.saving.set(true);
    this.clearFeedback();

    try {
      const product = await action();
      this.products.update((products) => [
        ...products.filter((currentProduct) => currentProduct.id !== product.id),
        product,
      ]);
      this.successMessage.set(successMessage(product));
      return product;
    } catch (error: unknown) {
      this.mutationError.set(toUserMessage(error, fallbackMessage));
      return null;
    } finally {
      this.saving.set(false);
    }
  }
}

function compareCategories(left: ProductCategory, right: ProductCategory): number {
  return left.name.localeCompare(right.name, 'es') || left.id.localeCompare(right.id);
}
