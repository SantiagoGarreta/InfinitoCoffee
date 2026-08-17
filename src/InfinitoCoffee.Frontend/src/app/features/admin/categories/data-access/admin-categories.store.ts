import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../../core/http/api-error.utils';
import {
  ProductCategoriesApiService,
  SaveProductCategoryRequest,
} from '../../../../core/product-categories/data-access/product-categories-api.service';
import { ProductCategory } from '../../../../core/product-categories/models/product-category.model';

@Injectable({ providedIn: 'root' })
export class AdminCategoriesStore {
  readonly categories = signal<ProductCategory[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly mutationError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly sortedCategories = computed(() => [...this.categories()].sort((left, right) =>
    left.name.localeCompare(right.name, 'es') || left.id.localeCompare(right.id)));

  private readonly categoriesApiService = inject(ProductCategoriesApiService);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      this.categories.set(await this.categoriesApiService.getProductCategories());
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar las categorías.'));
    } finally {
      this.loading.set(false);
    }
  }

  create(request: SaveProductCategoryRequest): Promise<ProductCategory | null> {
    return this.runMutation(
      () => this.categoriesApiService.createProductCategory(request),
      (category) => `Categoría "${category.name}" creada.`,
      'No fue posible crear la categoría.',
    );
  }

  update(categoryId: string, request: SaveProductCategoryRequest): Promise<ProductCategory | null> {
    return this.runMutation(
      () => this.categoriesApiService.updateProductCategory(categoryId, request),
      (category) => `Categoría "${category.name}" actualizada.`,
      'No fue posible actualizar la categoría.',
    );
  }

  activate(categoryId: string): Promise<ProductCategory | null> {
    return this.runMutation(
      () => this.categoriesApiService.activateProductCategory(categoryId),
      (category) => `Categoría "${category.name}" activada.`,
      'No fue posible activar la categoría.',
    );
  }

  deactivate(categoryId: string): Promise<ProductCategory | null> {
    return this.runMutation(
      () => this.categoriesApiService.deactivateProductCategory(categoryId),
      (category) => `Categoría "${category.name}" desactivada.`,
      'No fue posible desactivar la categoría.',
    );
  }

  clearFeedback(): void {
    this.mutationError.set(null);
    this.successMessage.set(null);
  }

  private async runMutation(
    action: () => Promise<ProductCategory>,
    successMessage: (category: ProductCategory) => string,
    fallbackMessage: string,
  ): Promise<ProductCategory | null> {
    this.saving.set(true);
    this.clearFeedback();

    try {
      const category = await action();
      this.categories.update((categories) => [
        ...categories.filter((currentCategory) => currentCategory.id !== category.id),
        category,
      ]);
      this.successMessage.set(successMessage(category));
      return category;
    } catch (error: unknown) {
      this.mutationError.set(toUserMessage(error, fallbackMessage));
      return null;
    } finally {
      this.saving.set(false);
    }
  }
}
