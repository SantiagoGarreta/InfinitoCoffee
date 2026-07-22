import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject } from '@angular/core';

import { KitchenOrdersStore } from '../data-access/kitchen-orders.store';

@Component({
  selector: 'app-kitchen-display-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './kitchen-display-page.component.html',
  styleUrl: './kitchen-display-page.component.scss',
})
export class KitchenDisplayPageComponent implements OnInit, OnDestroy {
  readonly store = inject(KitchenOrdersStore);
  readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly connectionState = computed(() => this.store.connectionState());
  readonly orders = computed(() => this.store.orders());
  readonly orderCount = computed(() => this.store.orderCount());

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
