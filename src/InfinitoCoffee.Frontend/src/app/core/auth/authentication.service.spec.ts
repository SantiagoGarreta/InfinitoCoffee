import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { OrdersRealtimeService } from '../realtime/orders-realtime.service';
import { AuthenticationState } from './authentication-state.service';
import { AuthenticationService, InvalidCredentialsError } from './authentication.service';
import { CsrfTokenStore } from './csrf-token.store';

describe('AuthenticationService', () => {
  let service: AuthenticationService;
  let http: HttpTestingController;
  let state: AuthenticationState;
  let csrf: CsrfTokenStore;
  const realtime = { stop: vi.fn(() => Promise.resolve()) };

  beforeEach(() => {
    realtime.stop.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        AuthenticationService,
        AuthenticationState,
        CsrfTokenStore,
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: APP_RUNTIME_CONFIG,
          useValue: {
            apiBaseUrl: 'http://api.test',
            signalRHubUrl: 'http://api.test/hubs/orders',
            pickupSignalRHubUrl: 'http://api.test/hubs/pickup',
          },
        },
        { provide: OrdersRealtimeService, useValue: realtime },
      ],
    });
    service = TestBed.inject(AuthenticationService);
    http = TestBed.inject(HttpTestingController);
    state = TestBed.inject(AuthenticationState);
    csrf = TestBed.inject(CsrfTokenStore);
  });

  afterEach(() => http.verify());

  async function waitForRequest(url: string): Promise<TestRequest> {
    let request: TestRequest | undefined;
    await vi.waitFor(() => {
      const matches = http.match(url);
      expect(matches).toHaveLength(1);
      request = matches[0];
    });
    return request!;
  }

  it('bootstraps an authenticated session and its csrf token', async () => {
    const initialization = service.initialize();
    http.expectOne('http://api.test/api/auth/me').flush({
      id: 'user-1', username: 'admin', displayName: 'Admin', role: 'Administrator',
    });
    (await waitForRequest('http://api.test/api/auth/csrf')).flush({ token: 'authenticated-token' });

    await initialization;

    expect(state.currentUser()?.username).toBe('admin');
    expect(state.isInitialized()).toBe(true);
    expect(csrf.getToken()).toBe('authenticated-token');
  });

  it('bootstraps anonymously after me returns 401', async () => {
    const initialization = service.initialize();
    http.expectOne('http://api.test/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });
    (await waitForRequest('http://api.test/api/auth/csrf')).flush({ token: 'anonymous-token' });

    await initialization;

    expect(state.currentUser()).toBeNull();
    expect(csrf.getToken()).toBe('anonymous-token');
  });

  it('refreshes csrf before and after a successful login', async () => {
    const login = service.login('cashier', 'password');
    http.expectOne('http://api.test/api/auth/csrf').flush({ token: 'anonymous-token' });
    (await waitForRequest('http://api.test/api/auth/login')).flush({
      id: 'user-2', username: 'cashier', displayName: 'Caja', role: 'Cashier',
    });
    (await waitForRequest('http://api.test/api/auth/csrf')).flush({ token: 'authenticated-token' });

    const user = await login;

    expect(user.role).toBe('Cashier');
    expect(state.currentUser()).toEqual(user);
    expect(csrf.getToken()).toBe('authenticated-token');
  });

  it('maps login 401 to a generic invalid credentials error', async () => {
    const login = service.login('unknown', 'password');
    http.expectOne('http://api.test/api/auth/csrf').flush({ token: 'anonymous-token' });
    (await waitForRequest('http://api.test/api/auth/login')).flush({}, { status: 401, statusText: 'Unauthorized' });

    await expect(login).rejects.toBeInstanceOf(InvalidCredentialsError);
    expect(state.currentUser()).toBeNull();
  });

  it('stops private realtime and clears memory after logout', async () => {
    state.setUser({ id: 'user-1', username: 'admin', displayName: 'Admin', role: 'Administrator' });
    csrf.setToken('authenticated-token');
    const logout = service.logout();
    http.expectOne('http://api.test/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });

    await logout;

    expect(realtime.stop).toHaveBeenCalledOnce();
    expect(state.currentUser()).toBeNull();
    expect(csrf.getToken()).toBeNull();
  });
});
