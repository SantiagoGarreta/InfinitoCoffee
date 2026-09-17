import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProductCategory } from '../../../../core/product-categories/models/product-category.model';
import { ErrorMessageComponent } from '../../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../../shared/ui/loading-state/loading-state.component';
import { AdminCategoriesStore } from '../data-access/admin-categories.store';

@Component({
  selector: 'app-admin-categories-page',
  standalone: true,
  imports: [CommonModule, ErrorMessageComponent, FormsModule, LoadingStateComponent],
  templateUrl: './admin-categories-page.component.html',
  styleUrl: './admin-categories-page.component.scss',
})
export class AdminCategoriesPageComponent implements OnInit {
  readonly store = inject(AdminCategoriesStore);
  readonly editingCategoryId = signal<string | null>(null);
  readonly confirmingDeactivateId = signal<string | null>(null);
  readonly name = signal('');
  readonly canSubmit = computed(() => {
    const name = this.name().trim();
    return name.length > 0 && name.length <= 150 && !this.store.saving();
  });
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  ngOnInit(): void {
    if (this.isBrowser) void this.store.load();
  }

  startCreate(): void {
    this.store.clearFeedback();
    this.editingCategoryId.set(null);
    this.confirmingDeactivateId.set(null);
    this.name.set('');
  }

  startEdit(category: ProductCategory): void {
    this.store.clearFeedback();
    this.confirmingDeactivateId.set(null);
    this.editingCategoryId.set(category.id);
    this.name.set(category.name);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit()) return;
    const categoryId = this.editingCategoryId();
    const request = { name: this.name().trim() };
    const result = categoryId
      ? await this.store.update(categoryId, request)
      : await this.store.create(request);
    if (result) {
      this.editingCategoryId.set(null);
      this.name.set('');
    }
  }

  async activate(categoryId: string): Promise<void> {
    this.confirmingDeactivateId.set(null);
    await this.store.activate(categoryId);
  }

  askToDeactivate(categoryId: string): void {
    this.store.clearFeedback();
    this.confirmingDeactivateId.set(categoryId);
  }

  cancelDeactivate(): void { this.confirmingDeactivateId.set(null); }

  async confirmDeactivate(categoryId: string): Promise<void> {
    if (await this.store.deactivate(categoryId)) this.confirmingDeactivateId.set(null);
  }
}
