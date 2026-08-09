import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';

import { AuthenticationState } from './authentication-state.service';
import { authGuard, guestGuard, roleGuard, roleHomeGuard } from './auth.guards';

describe('authentication guards', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AuthenticationState, provideRouter([])] });
  });

  it('sends anonymous users to login', () => {
    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
    expect((result as UrlTree).toString()).toBe('/login');
  });

  it.each([
    ['Administrator', '/orders/new'],
    ['Cashier', '/orders/new'],
    ['Kitchen', '/kitchen'],
  ] as const)('routes %s to its role home', (role, expectedUrl) => {
    TestBed.inject(AuthenticationState).setUser({
      id: 'user-1', username: 'user', displayName: 'User', role,
    });
    const result = TestBed.runInInjectionContext(() => roleHomeGuard({} as never, {} as never));
    expect((result as UrlTree).toString()).toBe(expectedUrl);
  });

  it('redirects an authenticated guest away from login', () => {
    TestBed.inject(AuthenticationState).setUser({
      id: 'user-1', username: 'kitchen', displayName: 'Kitchen', role: 'Kitchen',
    });
    const result = TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect((result as UrlTree).toString()).toBe('/kitchen');
  });

  it('redirects a cashier away from kitchen', () => {
    TestBed.inject(AuthenticationState).setUser({
      id: 'user-1', username: 'cashier', displayName: 'Cashier', role: 'Cashier',
    });
    const result = TestBed.runInInjectionContext(() => roleGuard('Administrator', 'Kitchen')({} as never, {} as never));
    expect((result as UrlTree).toString()).toBe('/orders/new');
  });
});
