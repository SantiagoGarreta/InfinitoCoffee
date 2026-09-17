import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { Product } from '../../../core/products/models/product.model';

@Component({
  selector: 'app-product-grid',
  standalone: true,
  imports: [CommonModule, CurrencyPipe],
  templateUrl: './product-grid.component.html',
  styleUrl: './product-grid.component.scss',
})
export class ProductGridComponent {
  readonly products = input.required<Product[]>();
  readonly disabled = input(false);
  readonly addProduct = output<Product>();
}
