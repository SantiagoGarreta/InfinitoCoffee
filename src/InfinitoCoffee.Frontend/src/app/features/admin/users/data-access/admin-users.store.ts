import { Injectable, computed, inject, signal } from '@angular/core';

import { toUserMessage } from '../../../../core/http/api-error.utils';
import { UsersApiService } from '../../../../core/users/data-access/users-api.service';
import {
  CreateUserRequest,
  UpdateUserRequest,
} from '../../../../core/users/models/user-requests.model';
import { User } from '../../../../core/users/models/user.model';

@Injectable({ providedIn: 'root' })
export class AdminUsersStore {
  readonly users = signal<User[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly mutationError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly sortedUsers = computed(() => [...this.users()].sort(compareUsers));

  private readonly usersApiService = inject(UsersApiService);

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      this.users.set(await this.usersApiService.getUsers());
    } catch (error: unknown) {
      this.loadError.set(toUserMessage(error, 'No fue posible cargar los usuarios.'));
    } finally {
      this.loading.set(false);
    }
  }

  create(request: CreateUserRequest): Promise<User | null> {
    return this.runUserMutation(
      () => this.usersApiService.createUser(request),
      (user) => `Usuario "${user.displayName}" creado.`,
      'No fue posible crear el usuario.',
    );
  }

  update(id: string, request: UpdateUserRequest): Promise<User | null> {
    return this.runUserMutation(
      () => this.usersApiService.updateUser(id, request),
      (user) => `Usuario "${user.displayName}" actualizado.`,
      'No fue posible actualizar el usuario.',
    );
  }

  activate(id: string): Promise<User | null> {
    return this.runUserMutation(
      () => this.usersApiService.activateUser(id),
      (user) => `Usuario "${user.displayName}" activado.`,
      'No fue posible activar el usuario.',
    );
  }

  deactivate(id: string): Promise<User | null> {
    return this.runUserMutation(
      () => this.usersApiService.deactivateUser(id),
      (user) => `Usuario "${user.displayName}" desactivado.`,
      'No fue posible desactivar el usuario.',
    );
  }

  async resetPassword(id: string, newPassword: string): Promise<boolean> {
    this.saving.set(true);
    this.clearFeedback();

    try {
      await this.usersApiService.resetPassword(id, { newPassword });
      const user = this.users().find((candidate) => candidate.id === id);
      this.successMessage.set(`Contraseña de "${user?.displayName ?? 'usuario'}" actualizada.`);
      return true;
    } catch (error: unknown) {
      this.mutationError.set(toUserMessage(error, 'No fue posible resetear la contraseña.'));
      return false;
    } finally {
      this.saving.set(false);
    }
  }

  clearFeedback(): void {
    this.mutationError.set(null);
    this.successMessage.set(null);
  }

  private async runUserMutation(
    action: () => Promise<User>,
    successMessage: (user: User) => string,
    fallbackMessage: string,
  ): Promise<User | null> {
    this.saving.set(true);
    this.clearFeedback();

    try {
      const user = await action();
      this.users.update((users) => [
        ...users.filter((candidate) => candidate.id !== user.id),
        user,
      ]);
      this.successMessage.set(successMessage(user));
      return user;
    } catch (error: unknown) {
      this.mutationError.set(toUserMessage(error, fallbackMessage));
      return null;
    } finally {
      this.saving.set(false);
    }
  }
}

function compareUsers(left: User, right: User): number {
  return Number(left.isSystemUser) - Number(right.isSystemUser)
    || left.displayName.localeCompare(right.displayName, 'es')
    || left.username.localeCompare(right.username, 'es')
    || left.id.localeCompare(right.id);
}
