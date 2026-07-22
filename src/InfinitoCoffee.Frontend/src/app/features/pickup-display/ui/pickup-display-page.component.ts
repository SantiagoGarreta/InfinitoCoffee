import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject } from '@angular/core';

import { PickupOrdersStore } from '../data-access/pickup-orders.store';

@Component({
  selector: 'app-pickup-display-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pickup-display-page.component.html',
  styleUrl: './pickup-display-page.component.scss',
})
export class PickupDisplayPageComponent implements OnInit, OnDestroy {
  readonly store = inject(PickupOrdersStore);
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
