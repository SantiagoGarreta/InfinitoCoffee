import { CommonModule } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProductCategory } from '../../../core/product-categories/models/product-category.model';
import { Product } from '../../../core/products/models/product.model';
import { ErrorMessageComponent } from '../../../shared/ui/error-message/error-message.component';
import { SaveProductRequest } from '../../../core/products/data-access/products-api.service';

@Component({
  selector: 'app-menu-editor',
  standalone: true,
  imports: [CommonModule, ErrorMessageComponent, FormsModule],
  templateUrl: './menu-editor.component.html',
  styleUrl: './menu-editor.component.scss',
})
export class MenuEditorComponent {
  readonly categories = input.required<ProductCategory[]>();
  readonly products = input.required<Product[]>();
  readonly saving = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly successMessage = input<string | null>(null);

  readonly close = output<void>();
  readonly createProduct = output<SaveProductRequest>();
  readonly updateProduct = output<{ productId: string; request: SaveProductRequest }>();
  readonly deleteProduct = output<string>();

  readonly editingProductId = signal<string | null>(null);
  readonly name = signal('');
  readonly description = signal('');
  readonly price = signal<number | null>(null);
  readonly categoryId = signal('');

  readonly sortedProducts = computed(() => [...this.products()].sort((left, right) =>
    left.name.localeCompare(right.name, 'es')));
  readonly categoryNameById = computed(() => Object.fromEntries(this.categories().map((category) => [category.id, category.name])));
  readonly isEditing = computed(() => this.editingProductId() !== null);
  readonly formTitle = computed(() => this.isEditing() ? 'Editar producto' : 'Agregar producto');
  readonly submitLabel = computed(() => this.isEditing() ? 'Guardar cambios' : 'Agregar producto');
  readonly canSubmit = computed(() =>
    this.name().trim().length > 0
    && this.categoryId().trim().length > 0
    && this.price() !== null
    && this.price()! > 0,
  );

  constructor() {
    effect(() => {
      const categories = this.categories();
      const currentCategoryId = this.categoryId();

      if (categories.length === 0) {
        this.categoryId.set('');
        return;
      }

      const categoryStillExists = categories.some((category) => category.id === currentCategoryId);
      if (!categoryStillExists && !this.isEditing()) {
        this.categoryId.set(categories[0]!.id);
      }
    });
  }

  startCreate(): void {
    this.editingProductId.set(null);
    this.name.set('');
    this.description.set('');
    this.price.set(null);
    this.categoryId.set(this.categories()[0]?.id ?? '');
  }

  startEdit(product: Product): void {
    this.editingProductId.set(product.id);
    this.name.set(product.name);
    this.description.set(product.description ?? '');
    this.price.set(product.price);
    this.categoryId.set(product.categoryId);
  }

  submit(): void {
    const request = this.buildRequest();
    if (!request) {
      return;
    }

    const editingProductId = this.editingProductId();
    if (editingProductId) {
      this.updateProduct.emit({ productId: editingProductId, request });
      return;
    }

    this.createProduct.emit(request);
  }

  remove(productId: string): void {
    this.deleteProduct.emit(productId);

    if (this.editingProductId() === productId) {
      this.startCreate();
    }
  }

  private buildRequest(): SaveProductRequest | null {
    const trimmedName = this.name().trim();
    const trimmedCategoryId = this.categoryId().trim();
    const price = this.price();

    if (trimmedName.length === 0 || trimmedCategoryId.length === 0 || price === null || price <= 0) {
      return null;
    }

    return {
      name: trimmedName,
      description: normalizeOptionalString(this.description()),
      price,
      categoryId: trimmedCategoryId,
    };
  }
}

function normalizeOptionalString(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : null;
}
