import { TestBed } from '@angular/core/testing';

import { UserRole } from '../../../../core/auth/models/authenticated-user.model';
import { UsersApiService } from '../../../../core/users/data-access/users-api.service';
import { CreateUserRequest, UpdateUserRequest } from '../../../../core/users/models/user-requests.model';
import { User } from '../../../../core/users/models/user.model';
import { AdminUsersStore } from './admin-users.store';

class FakeUsersApiService {
  users: User[] = [
    createUser('system', 'Cuenta del sistema', 'Administrator', true, true),
    createUser('kitchen', 'Cocina', 'Kitchen', false),
    createUser('admin', 'Administrador', 'Administrator'),
  ];
  error: unknown = null;
  updatePromise: Promise<User> | null = null;
  lastCreateRequest: CreateUserRequest | null = null;
  lastReset: { id: string; newPassword: string } | null = null;

  getUsers(): Promise<User[]> { return this.result(this.users); }
  createUser(request: CreateUserRequest): Promise<User> {
    this.lastCreateRequest = request;
    return this.result(createUser(`created-${request.role}`, request.displayName, request.role));
  }
  updateUser(id: string, request: UpdateUserRequest): Promise<User> {
    if (this.updatePromise) return this.updatePromise;
    return this.result({ ...this.users.find((user) => user.id === id)!, ...request });
  }
  activateUser(id: string): Promise<User> {
    return this.result({ ...this.users.find((user) => user.id === id)!, isActive: true });
  }
  deactivateUser(id: string): Promise<User> {
    return this.result({ ...this.users.find((user) => user.id === id)!, isActive: false });
  }
  resetPassword(id: string, request: { newPassword: string }): Promise<void> {
    this.lastReset = { id, newPassword: request.newPassword };
    return this.error ? Promise.reject(this.error) : Promise.resolve();
  }
  private result<T>(value: T): Promise<T> { return this.error ? Promise.reject(this.error) : Promise.resolve(value); }
}

describe('AdminUsersStore', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [
      AdminUsersStore,
      { provide: UsersApiService, useClass: FakeUsersApiService },
    ],
  }));

  it('loads users, sorts normal users by display name and leaves SystemUser last', async () => {
    const store = TestBed.inject(AdminUsersStore);
    await store.load();
    expect(store.sortedUsers().map((user) => user.username)).toEqual(['admin', 'kitchen', 'system']);
    expect(store.sortedUsers().at(-1)?.isSystemUser).toBe(true);
  });

  it.each(['Administrator', 'Cashier', 'Kitchen'] as const)('creates and reorders a %s user', async (role) => {
    const store = TestBed.inject(AdminUsersStore);
    const api = TestBed.inject(UsersApiService) as unknown as FakeUsersApiService;
    await store.load();
    const result = await store.create({
      username: `new.${role.toLowerCase()}`,
      displayName: `A ${role}`,
      password: ' preserved ',
      role,
    });
    expect(result?.role).toBe(role);
    expect(api.lastCreateRequest?.password).toBe(' preserved ');
    expect(store.sortedUsers()[0]?.displayName).toBe(`A ${role}`);
    expect(store.successMessage()).toContain('creado');
  });

  it('updates, activates and deactivates from backend responses', async () => {
    const store = TestBed.inject(AdminUsersStore);
    await store.load();
    await store.update('kitchen', { username: 'kitchen.two', displayName: 'Cocina Dos', role: 'Cashier' });
    expect(store.users().find((user) => user.id === 'kitchen')?.role).toBe('Cashier');
    await store.activate('kitchen');
    expect(store.users().find((user) => user.id === 'kitchen')?.isActive).toBe(true);
    await store.deactivate('admin');
    expect(store.users().find((user) => user.id === 'admin')?.isActive).toBe(false);
  });

  it('resets password without changing the collection', async () => {
    const store = TestBed.inject(AdminUsersStore);
    const api = TestBed.inject(UsersApiService) as unknown as FakeUsersApiService;
    await store.load();
    const before = store.users();
    expect(await store.resetPassword('kitchen', ' new password ')).toBe(true);
    expect(api.lastReset).toEqual({ id: 'kitchen', newPassword: ' new password ' });
    expect(store.users()).toBe(before);
    expect(store.successMessage()).toContain('actualizada');
  });

  it('does not update optimistically while the backend request is pending', async () => {
    const store = TestBed.inject(AdminUsersStore);
    const api = TestBed.inject(UsersApiService) as unknown as FakeUsersApiService;
    await store.load();
    let resolveUpdate!: (user: User) => void;
    api.updatePromise = new Promise((resolve) => { resolveUpdate = resolve; });
    const mutation = store.update('admin', { username: 'admin.two', displayName: 'Admin Dos', role: 'Administrator' });
    expect(store.users().find((user) => user.id === 'admin')?.username).toBe('admin');
    resolveUpdate(createUser('admin', 'Admin Dos', 'Administrator'));
    await mutation;
    expect(store.users().find((user) => user.id === 'admin')?.username).toBe('admin');
    expect(store.users().find((user) => user.id === 'admin')?.displayName).toBe('Admin Dos');
  });

  it('exposes load and mutation errors and preserves the collection on rejection', async () => {
    const store = TestBed.inject(AdminUsersStore);
    const api = TestBed.inject(UsersApiService) as unknown as FakeUsersApiService;
    api.error = new Error('failed');
    await store.load();
    expect(store.loadError()).not.toBeNull();
    api.error = null;
    await store.load();
    const before = store.users();
    api.error = new Error('failed');
    expect(await store.deactivate('admin')).toBeNull();
    expect(store.users()).toBe(before);
    expect(store.mutationError()).not.toBeNull();
    expect(await store.resetPassword('admin', 'password')).toBe(false);
  });
});

function createUser(
  id: string,
  displayName: string,
  role: UserRole,
  isActive = true,
  isSystemUser = false,
): User {
  return { id, username: id, displayName, role, isActive, isSystemUser };
}
