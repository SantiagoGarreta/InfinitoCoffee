import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { RealtimeConnectionState } from '../../../core/realtime/realtime-connection-state';

@Component({
  selector: 'app-connection-status',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './connection-status.component.html',
  styleUrl: './connection-status.component.scss',
})
export class ConnectionStatusComponent {
  readonly state = input.required<RealtimeConnectionState>();
  readonly retry = output<void>();

  readonly label = computed(() => {
    switch (this.state()) {
      case 'connected':
        return 'Conectado';
      case 'connecting':
        return 'Conectando';
      case 'reconnecting':
        return 'Reconectando';
      default:
        return 'Desconectado';
    }
  });
}
