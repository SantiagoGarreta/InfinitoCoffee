import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { ProductCategory } from '../../../core/product-categories/models/product-category.model';

@Component({
  selector: 'app-category-selector',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './category-selector.component.html',
  styleUrl: './category-selector.component.scss',
})
export class CategorySelectorComponent {
  readonly categories = input.required<ProductCategory[]>();
  readonly selectedCategoryId = input<string | null>(null);
  readonly selectCategory = output<string>();
}
