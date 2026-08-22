import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

import { PickupOrder } from '../../../core/pickup/models/pickup-order.model';

@Component({
  selector: 'app-pickup-order-number',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pickup-order-number.component.html',
  styleUrl: './pickup-order-number.component.scss',
})
export class PickupOrderNumberComponent {
  readonly order = input.required<PickupOrder>();
  readonly ready = input(false);

  formatItems(): string {
    return this.order().items
      .map((item) => `${item.quantity} ${item.productName}`)
      .join(' · ');
  }
}
