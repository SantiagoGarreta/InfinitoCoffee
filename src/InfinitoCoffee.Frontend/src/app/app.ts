import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthenticationState } from './core/auth/authentication-state.service';
import { AuthenticationService } from './core/auth/authentication.service';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly authenticationState = inject(AuthenticationState);
  readonly currentUser = this.authenticationState.currentUser;
  readonly isAdministrator = computed(() => this.authenticationState.role() === 'Administrator');
  readonly isCashier = computed(() => this.authenticationState.role() === 'Cashier');
  readonly isKitchen = computed(() => this.authenticationState.role() === 'Kitchen');
  readonly loggingOut = signal(false);
  readonly logoutError = signal<string | null>(null);

  private readonly authenticationService = inject(AuthenticationService);
  private readonly router = inject(Router);

  async logout(): Promise<void> {
    if (this.loggingOut()) {
      return;
    }

    this.loggingOut.set(true);
    this.logoutError.set(null);

    try {
      await this.authenticationService.logout();
      await this.router.navigate(['/login'], { replaceUrl: true });
    } catch {
      this.logoutError.set('No fue posible cerrar la sesión. Intentá nuevamente.');
    } finally {
      this.loggingOut.set(false);
    }
  }
}
