import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

import { PickupOrder } from '../../../core/pickup/models/pickup-order.model';
import { PickupOrderNumberComponent } from './pickup-order-number.component';

@Component({
  selector: 'app-pickup-section',
  standalone: true,
  imports: [CommonModule, PickupOrderNumberComponent],
  templateUrl: './pickup-section.component.html',
  styleUrl: './pickup-section.component.scss',
})
export class PickupSectionComponent {
  readonly title = input.required<string>();
  readonly orders = input.required<PickupOrder[]>();
  readonly ready = input(false);
}
