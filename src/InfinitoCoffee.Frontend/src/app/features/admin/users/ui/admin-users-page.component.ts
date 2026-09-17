import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthenticationState } from '../../../../core/auth/authentication-state.service';
import { AuthenticationService } from '../../../../core/auth/authentication.service';
import { UserRole } from '../../../../core/auth/models/authenticated-user.model';
import { User } from '../../../../core/users/models/user.model';
import { ErrorMessageComponent } from '../../../../shared/ui/error-message/error-message.component';
import { LoadingStateComponent } from '../../../../shared/ui/loading-state/loading-state.component';
import { AdminUsersStore } from '../data-access/admin-users.store';

const usernamePattern = /^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$/;

@Component({
  selector: 'app-admin-users-page',
  standalone: true,
  imports: [CommonModule, ErrorMessageComponent, FormsModule, LoadingStateComponent],
  templateUrl: './admin-users-page.component.html',
  styleUrl: './admin-users-page.component.scss',
})
export class AdminUsersPageComponent implements OnInit {
  readonly store = inject(AdminUsersStore);
  readonly currentUser = inject(AuthenticationState).currentUser;
  readonly roles: readonly UserRole[] = ['Administrator', 'Cashier', 'Kitchen'];
  readonly editingUserId = signal<string | null>(null);
  readonly confirmingDeactivateId = signal<string | null>(null);
  readonly resettingPasswordId = signal<string | null>(null);
  readonly username = signal('');
  readonly displayName = signal('');
  readonly password = signal('');
  readonly role = signal<UserRole>('Cashier');
  readonly newPassword = signal('');
  readonly sessionRenewalRequired = signal(false);
  readonly sessionLogoutError = signal<string | null>(null);

  readonly editingUser = computed(() => this.store.users()
    .find((user) => user.id === this.editingUserId()));
  readonly isSelfEditing = computed(() => this.isCurrentUser(this.editingUser()));
  readonly mutationsBlocked = computed(() => this.store.saving() || this.sessionRenewalRequired());
  readonly usernameIsValid = computed(() => {
    const username = this.username().trim();
    return username.length >= 3 && username.length <= 50 && usernamePattern.test(username);
  });
  readonly displayNameIsValid = computed(() => {
    const displayName = this.displayName().trim();
    return displayName.length > 0 && displayName.length <= 100;
  });
  readonly passwordIsValid = computed(() => isValidPassword(this.password()));
  readonly newPasswordIsValid = computed(() => isValidPassword(this.newPassword()));
  readonly canSubmit = computed(() => {
    if (this.mutationsBlocked() || !this.usernameIsValid() || !this.displayNameIsValid()) {
      return false;
    }

    const editingUser = this.editingUser();
    if (!editingUser) {
      return this.passwordIsValid() && this.roles.includes(this.role());
    }

    const role = this.isCurrentUser(editingUser) ? editingUser.role : this.role();
    return this.username().trim() !== editingUser.username
      || this.displayName().trim() !== editingUser.displayName
      || role !== editingUser.role;
  });
  readonly canResetPassword = computed(() => this.newPasswordIsValid() && !this.mutationsBlocked());

  private readonly authenticationService = inject(AuthenticationService);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  ngOnInit(): void {
    if (this.isBrowser) {
      void this.store.load().then(() => this.resetForm(false));
    }
  }

  isCurrentUser(user: User | undefined): boolean {
    return !!user && user.id === this.currentUser()?.id;
  }

  startCreate(): void {
    if (this.sessionRenewalRequired()) return;
    this.resetForm();
  }

  startEdit(user: User): void {
    if (user.isSystemUser || this.sessionRenewalRequired()) return;
    this.store.clearFeedback();
    this.confirmingDeactivateId.set(null);
    this.closePasswordReset();
    this.editingUserId.set(user.id);
    this.username.set(user.username);
    this.displayName.set(user.displayName);
    this.role.set(user.role);
    this.password.set('');
  }

  cancelForm(): void {
    this.resetForm();
  }

  async submit(): Promise<void> {
    if (!this.canSubmit()) return;

    const editingUser = this.editingUser();
    if (!editingUser) {
      const created = await this.store.create({
        username: this.username().trim(),
        displayName: this.displayName().trim(),
        password: this.password(),
        role: this.role(),
      });
      if (created) this.resetForm(false);
      return;
    }

    const isSelf = this.isCurrentUser(editingUser);
    const updated = await this.store.update(editingUser.id, {
      username: this.username().trim(),
      displayName: this.displayName().trim(),
      role: isSelf ? editingUser.role : this.role(),
    });
    if (!updated) return;

    if (isSelf) {
      await this.logoutAfterSelfMutation();
    } else {
      this.resetForm(false);
    }
  }

  async activate(user: User): Promise<void> {
    if (!this.canMutateUser(user) || this.isCurrentUser(user)) return;
    this.confirmingDeactivateId.set(null);
    await this.store.activate(user.id);
  }

  askToDeactivate(user: User): void {
    if (!this.canMutateUser(user) || this.isCurrentUser(user)) return;
    this.store.clearFeedback();
    this.closePasswordReset();
    this.confirmingDeactivateId.set(user.id);
  }

  cancelDeactivate(): void {
    this.confirmingDeactivateId.set(null);
  }

  async confirmDeactivate(user: User): Promise<void> {
    if (this.confirmingDeactivateId() !== user.id || !this.canMutateUser(user)) return;
    if (await this.store.deactivate(user.id)) this.confirmingDeactivateId.set(null);
  }

  openPasswordReset(user: User): void {
    if (!this.canMutateUser(user)) return;
    this.store.clearFeedback();
    this.confirmingDeactivateId.set(null);
    this.resettingPasswordId.set(user.id);
    this.newPassword.set('');
  }

  closePasswordReset(): void {
    this.resettingPasswordId.set(null);
    this.newPassword.set('');
  }

  async confirmPasswordReset(user: User): Promise<void> {
    if (this.resettingPasswordId() !== user.id || !this.canResetPassword() || !this.canMutateUser(user)) return;
    const reset = await this.store.resetPassword(user.id, this.newPassword());
    if (!reset) return;

    this.closePasswordReset();
    if (this.isCurrentUser(user)) await this.logoutAfterSelfMutation();
  }

  async retryLogout(): Promise<void> {
    if (!this.sessionRenewalRequired()) return;
    await this.logoutAfterSelfMutation();
  }

  private canMutateUser(user: User): boolean {
    return !user.isSystemUser && !this.mutationsBlocked();
  }

  private async logoutAfterSelfMutation(): Promise<void> {
    try {
      await this.authenticationService.logout();
      await this.router.navigate(['/login'], { replaceUrl: true });
    } catch {
      this.sessionRenewalRequired.set(true);
      this.sessionLogoutError.set(
        'El cambio fue guardado, pero no fue posible cerrar la sesión. Reintentá el cierre antes de continuar.',
      );
    }
  }

  private resetForm(clearFeedback = true): void {
    if (clearFeedback) this.store.clearFeedback();
    this.editingUserId.set(null);
    this.confirmingDeactivateId.set(null);
    this.closePasswordReset();
    this.username.set('');
    this.displayName.set('');
    this.password.set('');
    this.role.set('Cashier');
  }
}

function isValidPassword(password: string): boolean {
  return password.length > 0 && password.length <= 256 && password.trim().length > 0;
}
