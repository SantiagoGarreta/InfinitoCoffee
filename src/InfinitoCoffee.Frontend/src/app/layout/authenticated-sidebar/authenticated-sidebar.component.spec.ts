import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthenticationState } from '../../core/auth/authentication-state.service';
import { AuthenticationService } from '../../core/auth/authentication.service';
import { UserRole } from '../../core/auth/models/authenticated-user.model';
import { AuthenticatedSidebarComponent } from './authenticated-sidebar.component';

describe('AuthenticatedSidebarComponent', () => {
  const authService = { logout: vi.fn<() => Promise<void>>() };

  beforeEach(async () => {
    authService.logout.mockReset().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({
      imports: [AuthenticatedSidebarComponent],
      providers: [
        AuthenticationState,
        provideRouter([]),
        { provide: AuthenticationService, useValue: authService },
      ],
    }).compileComponents();
  });

  it.each([
    ['Administrator', ['Inicio', 'Caja', 'Cocina', 'Pickup', 'Resultado', 'Productos', 'Categorias', 'Usuarios'], []],
    ['Cashier', ['Caja', 'Cocina', 'Pickup'], ['Inicio', 'Resultado', 'Productos', 'Categorias', 'Usuarios']],
    ['Kitchen', ['Cocina', 'Pickup'], ['Inicio', 'Resultado', 'Caja', 'Productos', 'Categorias', 'Usuarios']],
  ] as const)('shows exact navigation and identity for %s', (role, visible, hidden) => {
    const fixture = create(role);
    const text = fixture.nativeElement.textContent as string;
    visible.forEach((label) => expect(text).toContain(label));
    hidden.forEach((label) => expect(text).not.toContain(label));
    expect(text).toContain('Displayed User');
    expect(text).toContain(role);
    expect(text).toContain('Cerrar sesión');

    const pickup = [...fixture.nativeElement.querySelectorAll('a')]
      .find((anchor: HTMLAnchorElement) => anchor.textContent?.includes('Pickup')) as HTMLAnchorElement;
    expect(pickup.getAttribute('href')).toBe('/pickup');
    expect(pickup.getAttribute('target')).toBe('_blank');
    expect(pickup.getAttribute('rel')).toContain('noopener');
  });

  it('uses username when display name is empty', () => {
    TestBed.inject(AuthenticationState).setUser({ id: '1', username: 'fallback.user', displayName: '', role: 'Cashier' });
    const fixture = TestBed.createComponent(AuthenticatedSidebarComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('fallback.user');
  });

  it('calls logout once, prevents a concurrent call and returns to login', async () => {
    let resolveLogout!: () => void;
    authService.logout.mockReturnValue(new Promise<void>((resolve) => { resolveLogout = resolve; }));
    const fixture = create('Administrator');
    const component = fixture.componentInstance;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const first = component.logout();
    const second = component.logout();
    expect(authService.logout).toHaveBeenCalledTimes(1);
    resolveLogout();
    await Promise.all([first, second]);
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });

  it('reports logout failure accessibly', async () => {
    authService.logout.mockRejectedValue(new Error('offline'));
    const fixture = create('Administrator');
    await fixture.componentInstance.logout();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('No fue posible');
  });

  function create(role: UserRole) {
    TestBed.inject(AuthenticationState).setUser({ id: '1', username: 'user', displayName: 'Displayed User', role });
    const fixture = TestBed.createComponent(AuthenticatedSidebarComponent);
    fixture.detectChanges();
    return fixture;
  }
});
