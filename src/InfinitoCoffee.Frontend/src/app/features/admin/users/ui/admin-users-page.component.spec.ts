import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthenticationState } from '../../../../core/auth/authentication-state.service';
import { AuthenticationService } from '../../../../core/auth/authentication.service';
import { UserRole } from '../../../../core/auth/models/authenticated-user.model';
import { CreateUserRequest, UpdateUserRequest } from '../../../../core/users/models/user-requests.model';
import { User } from '../../../../core/users/models/user.model';
import { AdminUsersStore } from '../data-access/admin-users.store';
import { AdminUsersPageComponent } from './admin-users-page.component';

class FakeAdminUsersStore {
  readonly users = signal<User[]>([
    user('self', 'admin.self', 'Administrador Actual', 'Administrator'),
    user('active', 'cashier.one', 'Caja Uno', 'Cashier'),
    user('inactive', 'kitchen.one', 'Cocina Uno', 'Kitchen', false),
    user('system', 'system.admin', 'Cuenta del sistema', 'Administrator', true, true),
  ]);
  readonly sortedUsers = signal(this.users());
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly mutationError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  loadCalls = 0;
  lastCreate: CreateUserRequest | null = null;
  lastUpdate: { id: string; request: UpdateUserRequest } | null = null;
  activatedId: string | null = null;
  deactivatedId: string | null = null;
  lastReset: { id: string; password: string } | null = null;

  load(): Promise<void> { this.loadCalls++; return Promise.resolve(); }
  clearFeedback(): void { this.mutationError.set(null); this.successMessage.set(null); }
  create(request: CreateUserRequest): Promise<User | null> {
    this.lastCreate = request;
    return Promise.resolve(user('created', request.username, request.displayName, request.role));
  }
  update(id: string, request: UpdateUserRequest): Promise<User | null> {
    this.lastUpdate = { id, request };
    return Promise.resolve({ ...this.users().find((candidate) => candidate.id === id)!, ...request });
  }
  activate(id: string): Promise<User | null> {
    this.activatedId = id;
    return Promise.resolve({ ...this.users().find((candidate) => candidate.id === id)!, isActive: true });
  }
  deactivate(id: string): Promise<User | null> {
    this.deactivatedId = id;
    return Promise.resolve({ ...this.users().find((candidate) => candidate.id === id)!, isActive: false });
  }
  resetPassword(id: string, password: string): Promise<boolean> {
    this.lastReset = { id, password };
    return Promise.resolve(true);
  }
}

describe('AdminUsersPageComponent', () => {
  const authService = { logout: vi.fn<() => Promise<void>>() };

  beforeEach(async () => {
    authService.logout.mockReset().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({
      imports: [AdminUsersPageComponent],
      providers: [
        AuthenticationState,
        provideRouter([]),
        { provide: AuthenticationService, useValue: authService },
        { provide: AdminUsersStore, useClass: FakeAdminUsersStore },
      ],
    }).compileComponents();
    TestBed.inject(AuthenticationState).setUser({
      id: 'self', username: 'admin.self', displayName: 'Administrador Actual', role: 'Administrator',
    });
  });

  it('renders identity, roles, states and self/system badges without Delete', async () => {
    const fixture = await createFixture();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Administrador Actual');
    expect(text).toContain('@admin.self');
    expect(text).toContain('Cashier · Activo');
    expect(text).toContain('Kitchen · Inactivo');
    expect(text).toContain('Tu cuenta');
    expect(text).toContain('Sistema');
    expect(text).not.toContain('Delete');
    expect(text).not.toContain('Eliminar');
  });

  it('shows loading, load error with retry, empty state and success feedback', async () => {
    const fixture = await createFixture();
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    store.loading.set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-loading-state')).not.toBeNull();
    store.loading.set(false);
    store.loadError.set('No disponible');
    fixture.detectChanges();
    const retry = fixture.nativeElement.querySelector('app-error-message button') as HTMLButtonElement;
    retry.click();
    expect(store.loadCalls).toBe(2);
    store.loadError.set(null);
    store.sortedUsers.set([]);
    store.successMessage.set('Operación exitosa.');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Todavía no hay usuarios');
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain('Operación exitosa');
  });

  it.each(['Administrator', 'Cashier', 'Kitchen'] as const)('creates a %s user and preserves password whitespace', async (role) => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    component.username.set(` new.${role.toLowerCase()} `);
    component.displayName.set(` Nuevo ${role} `);
    component.password.set(' password preserved ');
    component.role.set(role);
    await component.submit();
    expect(store.lastCreate).toEqual({
      username: `new.${role.toLowerCase()}`,
      displayName: `Nuevo ${role}`,
      password: ' password preserved ',
      role,
    });
    expect(component.password()).toBe('');
  });

  it('enforces exact username, display name and password validation with only approved roles', async () => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    component.username.set('.invalid');
    component.displayName.set('User');
    component.password.set('password');
    expect(component.canSubmit()).toBe(false);
    component.username.set('valid.user');
    component.displayName.set('   ');
    expect(component.canSubmit()).toBe(false);
    component.displayName.set('User');
    component.password.set('   ');
    expect(component.canSubmit()).toBe(false);
    component.password.set(' password ');
    expect(component.canSubmit()).toBe(true);
    expect(component.roles).toEqual(['Administrator', 'Cashier', 'Kitchen']);
  });

  it('edits another user including role and prevents a no-op submit', async () => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    component.startEdit(store.users()[1]!);
    expect(component.canSubmit()).toBe(false);
    component.username.set(' cashier.two ');
    component.displayName.set(' Caja Dos ');
    component.role.set('Kitchen');
    expect(component.canSubmit()).toBe(true);
    await component.submit();
    expect(store.lastUpdate).toEqual({
      id: 'active',
      request: { username: 'cashier.two', displayName: 'Caja Dos', role: 'Kitchen' },
    });
    expect(authService.logout).not.toHaveBeenCalled();
  });

  it('activates immediately and deactivates only after inline confirmation', async () => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    await component.activate(store.users()[2]!);
    expect(store.activatedId).toBe('inactive');
    component.askToDeactivate(store.users()[1]!);
    fixture.detectChanges();
    expect(store.deactivatedId).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Las sesiones que ya estén abiertas no se cierran inmediatamente');
    await component.confirmDeactivate(store.users()[1]!);
    expect(store.deactivatedId).toBe('active');
  });

  it('resets another password inline without logging out and preserves whitespace', async () => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    const other = store.users()[1]!;
    component.openPasswordReset(other);
    component.newPassword.set(' new password ');
    await component.confirmPasswordReset(other);
    expect(store.lastReset).toEqual({ id: other.id, password: ' new password ' });
    expect(component.newPassword()).toBe('');
    expect(authService.logout).not.toHaveBeenCalled();
  });

  it('shows no mutation actions for SystemUser and keeps it last', async () => {
    const fixture = await createFixture();
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    expect(store.sortedUsers().at(-1)?.isSystemUser).toBe(true);
    const systemCard = card(fixture, 'system');
    expect(systemCard.textContent).toContain('Cuenta técnica gestionada mediante herramientas del sistema');
    expect(systemCard.querySelectorAll('button')).toHaveLength(0);
  });

  it('makes self role read-only and omits self deactivate while keeping edit and reset', async () => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    const self = store.users()[0]!;
    const selfCard = card(fixture, 'self');
    expect(selfCard.querySelector('.action-edit')).not.toBeNull();
    expect(selfCard.querySelector('.action-reset')).not.toBeNull();
    expect(selfCard.querySelector('.action-deactivate')).toBeNull();
    component.startEdit(self);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('select[name="role"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('.role-readonly')?.textContent).toContain('Administrator');
    expect(fixture.nativeElement.textContent).toContain('Tu propio rol no puede modificarse desde esta sesión');
  });

  it.each(['username', 'displayName'] as const)('logs out and navigates to login after successful self %s edit', async (field) => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component.startEdit(store.users()[0]!);
    component[field].set(field === 'username' ? 'admin.renamed' : 'Administrador Renombrado');
    await component.submit();
    expect(store.lastUpdate?.request.role).toBe('Administrator');
    expect(authService.logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });

  it('logs out and navigates to login after successful self password reset', async () => {
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const self = store.users()[0]!;
    component.openPasswordReset(self);
    component.newPassword.set('self new password');
    await component.confirmPasswordReset(self);
    expect(store.lastReset?.id).toBe('self');
    expect(authService.logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });

  it('blocks mutations after logout failure and retries until login navigation succeeds', async () => {
    authService.logout.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(undefined);
    const fixture = await createFixture();
    const component = fixture.componentInstance;
    const store = TestBed.inject(AdminUsersStore) as unknown as FakeAdminUsersStore;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component.startEdit(store.users()[0]!);
    component.displayName.set('Nuevo nombre');
    await component.submit();
    fixture.detectChanges();
    expect(component.sessionRenewalRequired()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('El cambio fue guardado');
    expect(fixture.nativeElement.textContent).toContain('Reintentar cerrar sesión');
    expect((fixture.nativeElement.querySelector('.admin-users__form button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
    await component.retryLogout();
    expect(authService.logout).toHaveBeenCalledTimes(2);
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });

  async function createFixture() {
    const fixture = TestBed.createComponent(AdminUsersPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function card(fixture: Awaited<ReturnType<typeof createFixture>>, id: string): HTMLElement {
    return fixture.nativeElement.querySelector(`[data-user-id="${id}"]`) as HTMLElement;
  }
});

function user(
  id: string,
  username: string,
  displayName: string,
  role: UserRole,
  isActive = true,
  isSystemUser = false,
): User {
  return { id, username, displayName, role, isActive, isSystemUser };
}
