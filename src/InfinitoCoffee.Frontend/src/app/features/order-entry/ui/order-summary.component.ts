import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { EntryOrderItem } from '../data-access/order-entry.store';

@Component({
  selector: 'app-order-summary',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, FormsModule],
  templateUrl: './order-summary.component.html',
  styleUrl: './order-summary.component.scss',
})
export class OrderSummaryComponent {
  readonly items = input.required<EntryOrderItem[]>();
  readonly total = input.required<number>();
  readonly disabled = input(false);

  readonly increase = output<string>();
  readonly decrease = output<string>();
  readonly notesChanged = output<{ productId: string; notes: string }>();
}
