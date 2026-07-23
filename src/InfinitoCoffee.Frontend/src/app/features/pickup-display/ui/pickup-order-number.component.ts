import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

import { Order } from '../../../core/orders/models/order.model';

@Component({
  selector: 'app-pickup-order-number',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pickup-order-number.component.html',
  styleUrl: './pickup-order-number.component.scss',
})
export class PickupOrderNumberComponent {
  readonly order = input.required<Order>();
  readonly ready = input(false);
}
