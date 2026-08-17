import { Injectable, computed, signal } from '@angular/core';

import { AuthenticatedUser } from './models/authenticated-user.model';

@Injectable({ providedIn: 'root' })
export class AuthenticationState {
  private readonly user = signal<AuthenticatedUser | null>(null);
  private readonly initialized = signal(false);

  readonly currentUser = this.user.asReadonly();
  readonly isInitialized = this.initialized.asReadonly();
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly role = computed(() => this.user()?.role ?? null);

  setUser(user: AuthenticatedUser): void {
    this.user.set(user);
  }

  clearUser(): void {
    this.user.set(null);
  }

  markInitialized(): void {
    this.initialized.set(true);
  }
}
