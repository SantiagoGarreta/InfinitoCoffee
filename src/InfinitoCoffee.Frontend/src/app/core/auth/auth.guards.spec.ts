import { TestBed } from '@angular/core/testing';
import { provideRouter, UrlTree } from '@angular/router';

import { AuthenticationState } from './authentication-state.service';
import { authGuard, guestGuard, roleGuard, roleHomeGuard } from './auth.guards';
import { getRoleHome, roleHomeRedirect } from './auth-navigation';
import { UserRole } from './models/authenticated-user.model';

describe('authentication navigation and guards', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AuthenticationState, provideRouter([])] });
  });

  it.each([
    ['Administrator', '/admin'],
    ['Cashier', '/orders/new'],
    ['Kitchen', '/kitchen'],
  ] as const)('uses %s home as the single navigation source', (role, expected) => {
    expect(getRoleHome(role)).toBe(expected);
    setRole(role);
    expect(url(roleHomeGuard)).toBe(expected);
    expect(TestBed.runInInjectionContext(() => roleHomeRedirect({} as never))).toBe(expected);
    expect(url(guestGuard)).toBe(expected);
  });

  it('routes anonymous users to login from root and private guards', () => {
    expect(url(authGuard)).toBe('/login');
    expect(url(roleHomeGuard)).toBe('/login');
    expect(TestBed.runInInjectionContext(() => roleHomeRedirect({} as never))).toBe('/login');
    expect(guestGuardInContext()).toBe(true);
  });

  it.each([
    ['Administrator', ['admin', 'orders', 'kitchen']],
    ['Cashier', ['orders', 'kitchen']],
    ['Kitchen', ['kitchen']],
  ] as const)('enforces private destinations for %s', (role, allowed) => {
    setRole(role);
    const destinations = allowed as readonly string[];
    expect(guardResult(roleGuard('Administrator'))).toBe(destinations.includes('admin') ? true : getRoleHome(role));
    expect(guardResult(roleGuard('Administrator', 'Cashier'))).toBe(destinations.includes('orders') ? true : getRoleHome(role));
    expect(guardResult(roleGuard('Administrator', 'Cashier', 'Kitchen'))).toBe(destinations.includes('kitchen') ? true : getRoleHome(role));
  });

  function setRole(role: UserRole): void {
    TestBed.inject(AuthenticationState).setUser({ id: 'user-1', username: 'user', displayName: 'User', role });
  }

  function url(guard: typeof authGuard): string {
    return (TestBed.runInInjectionContext(() => guard({} as never, {} as never)) as UrlTree).toString();
  }

  function guestGuardInContext(): true | UrlTree {
    return TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never)) as true | UrlTree;
  }

  function guardResult(guard: ReturnType<typeof roleGuard>): true | string {
    const result = TestBed.runInInjectionContext(() => guard({} as never, {} as never));
    return result === true ? true : (result as UrlTree).toString();
  }
});
