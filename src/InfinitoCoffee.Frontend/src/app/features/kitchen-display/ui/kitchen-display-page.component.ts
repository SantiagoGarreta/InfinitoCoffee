import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject } from '@angular/core';

import { ConnectionStatusComponent } from '../../../shared/ui/connection-status/connection-status.component';
import { ErrorMessageComponent } from '../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state/loading-state.component';
import { KitchenOrdersStore } from '../data-access/kitchen-orders.store';
import { KitchenColumnComponent } from './kitchen-column.component';

@Component({
  selector: 'app-kitchen-display-page',
  standalone: true,
  imports: [
    CommonModule,
    ConnectionStatusComponent,
    ErrorMessageComponent,
    KitchenColumnComponent,
    LoadingStateComponent,
  ],
  templateUrl: './kitchen-display-page.component.html',
  styleUrl: './kitchen-display-page.component.scss',
})
export class KitchenDisplayPageComponent implements OnInit, OnDestroy {
  readonly store = inject(KitchenOrdersStore);
  readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly connectionState = computed(() => this.store.connectionState());
  readonly queuedOrders = computed(() => this.store.queuedOrders());
  readonly preparingOrders = computed(() => this.store.preparingOrders());
  readonly readyOrders = computed(() => this.store.readyOrders());

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    void this.store.initialize();
  }

  ngOnDestroy(): void {
    this.store.destroy();
  }
}
