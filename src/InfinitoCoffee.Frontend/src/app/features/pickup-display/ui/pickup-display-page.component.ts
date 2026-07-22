import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject } from '@angular/core';

import { ConnectionStatusComponent } from '../../../shared/ui/connection-status/connection-status.component';
import { ErrorMessageComponent } from '../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state/loading-state.component';
import { PickupOrdersStore } from '../data-access/pickup-orders.store';
import { PickupSectionComponent } from './pickup-section.component';

@Component({
  selector: 'app-pickup-display-page',
  standalone: true,
  imports: [
    CommonModule,
    ConnectionStatusComponent,
    ErrorMessageComponent,
    LoadingStateComponent,
    PickupSectionComponent,
  ],
  templateUrl: './pickup-display-page.component.html',
  styleUrl: './pickup-display-page.component.scss',
})
export class PickupDisplayPageComponent implements OnInit, OnDestroy {
  readonly store = inject(PickupOrdersStore);
  readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
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
