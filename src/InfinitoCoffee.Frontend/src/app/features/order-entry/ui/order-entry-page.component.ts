import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { OrderSource } from '../../../core/orders/models/order.model';
import { ORDER_NUMBER_MAX_LENGTH } from '../../../core/orders/order.constants';
import { ErrorMessageComponent } from '../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state/loading-state.component';
import { OrderEntryStore } from '../data-access/order-entry.store';
import { CategorySelectorComponent } from './category-selector.component';
import { OrderSummaryComponent } from './order-summary.component';
import { ProductGridComponent } from './product-grid.component';

@Component({
  selector: 'app-order-entry-page',
  standalone: true,
  imports: [
    CategorySelectorComponent,
    CommonModule,
    ErrorMessageComponent,
    FormsModule,
    LoadingStateComponent,
    OrderSummaryComponent,
    ProductGridComponent,
  ],
  templateUrl: './order-entry-page.component.html',
  styleUrl: './order-entry-page.component.scss',
})
export class OrderEntryPageComponent implements OnInit {
  readonly store = inject(OrderEntryStore);
  readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly orderSources: OrderSource[] = ['Counter', 'WhatsApp', 'Web'];
  readonly orderNumberMaxLength = ORDER_NUMBER_MAX_LENGTH;
  readonly trimmedOrderNumberLength = computed(() => this.store.orderNumber().trim().length);

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    void this.store.initialize();
  }
}
