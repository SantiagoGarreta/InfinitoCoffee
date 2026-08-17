import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';

import { AuthenticationState } from '../../core/auth/authentication-state.service';
import { AuthenticationService } from '../../core/auth/authentication.service';

@Component({
  selector: 'app-authenticated-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './authenticated-sidebar.component.html',
  styleUrl: './authenticated-sidebar.component.scss',
})
export class AuthenticatedSidebarComponent {
  private readonly authenticationState = inject(AuthenticationState);
  private readonly authenticationService = inject(AuthenticationService);
  private readonly router = inject(Router);

  readonly currentUser = this.authenticationState.currentUser;
  readonly role = this.authenticationState.role;
  readonly isAdministrator = computed(() => this.role() === 'Administrator');
  readonly isCashier = computed(() => this.role() === 'Cashier');
  readonly isKitchen = computed(() => this.role() === 'Kitchen');
  readonly loggingOut = signal(false);
  readonly logoutError = signal<string | null>(null);

  async logout(): Promise<void> {
    if (this.loggingOut()) return;

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
