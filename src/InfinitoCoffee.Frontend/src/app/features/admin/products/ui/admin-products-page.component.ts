import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ProductCategory } from '../../../../core/product-categories/models/product-category.model';
import { Product } from '../../../../core/products/models/product.model';
import { ErrorMessageComponent } from '../../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../../shared/ui/loading-state/loading-state.component';
import { AdminProductsStore } from '../data-access/admin-products.store';

@Component({
  selector: 'app-admin-products-page',
  standalone: true,
  imports: [CommonModule, ErrorMessageComponent, FormsModule, LoadingStateComponent, RouterLink],
  templateUrl: './admin-products-page.component.html',
  styleUrl: './admin-products-page.component.scss',
})
export class AdminProductsPageComponent implements OnInit {
  readonly store = inject(AdminProductsStore);
  readonly editingProductId = signal<string | null>(null);
  readonly confirmingDeactivateId = signal<string | null>(null);
  readonly name = signal('');
  readonly description = signal('');
  readonly price = signal<number | null>(null);
  readonly cost = signal<number | null>(null);
  readonly categoryId = signal('');

  readonly formCategories = computed(() => {
    const activeCategories = this.store.activeCategories();
    const editingProduct = this.editingProduct();
    if (!editingProduct) {
      return activeCategories;
    }

    const currentCategory = this.store.categoryFor(editingProduct.categoryId);
    if (!currentCategory || currentCategory.isActive) {
      return activeCategories;
    }

    return [currentCategory, ...activeCategories]
      .sort((left, right) => left.name.localeCompare(right.name, 'es') || left.id.localeCompare(right.id));
  });

  readonly canSubmit = computed(() => {
    const trimmedName = this.name().trim();
    const description = this.description().trim();
    const price = this.price();
    const cost = this.cost();
    return trimmedName.length > 0
      && trimmedName.length <= 150
      && description.length <= 1000
      && price !== null
      && price >= 0.01
      && cost !== null
      && cost >= 0
      && this.categoryId().length > 0
      && !this.store.saving();
  });

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  ngOnInit(): void {
    if (this.isBrowser) {
      void this.store.load().then(() => this.resetForm());
    }
  }

  editingProduct(): Product | undefined {
    const productId = this.editingProductId();
    return this.store.products().find((product) => product.id === productId);
  }

  categoryFor(product: Product): ProductCategory | undefined {
    return this.store.categoryFor(product.categoryId);
  }

  startCreate(): void {
    this.store.clearFeedback();
    this.resetForm();
  }

  startEdit(product: Product): void {
    this.store.clearFeedback();
    this.confirmingDeactivateId.set(null);
    this.editingProductId.set(product.id);
    this.name.set(product.name);
    this.description.set(product.description ?? '');
    this.price.set(product.price);
    this.cost.set(product.cost);
    this.categoryId.set(product.categoryId);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }

    const productId = this.editingProductId();
    const request = {
      name: this.name().trim(),
      description: normalizeOptionalString(this.description()),
      price: this.price()!,
      cost: this.cost()!,
      categoryId: this.categoryId(),
    };
    const result = productId
      ? await this.store.update(productId, request)
      : await this.store.create(request);

    if (result) {
      this.resetForm();
    }
  }

  async activate(productId: string): Promise<void> {
    this.confirmingDeactivateId.set(null);
    await this.store.activate(productId);
  }

  askToDeactivate(productId: string): void {
    this.store.clearFeedback();
    this.confirmingDeactivateId.set(productId);
  }

  cancelDeactivate(): void {
    this.confirmingDeactivateId.set(null);
  }

  async confirmDeactivate(productId: string): Promise<void> {
    const result = await this.store.deactivate(productId);
    if (result) {
      this.confirmingDeactivateId.set(null);
    }
  }

  private resetForm(): void {
    this.editingProductId.set(null);
    this.confirmingDeactivateId.set(null);
    this.name.set('');
    this.description.set('');
    this.price.set(null);
    this.cost.set(null);
    this.categoryId.set(this.store.activeCategories()[0]?.id ?? '');
  }
}

function normalizeOptionalString(value: string): string | null {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : null;
}
