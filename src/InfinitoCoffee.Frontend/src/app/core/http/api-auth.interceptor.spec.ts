import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthenticationState } from '../auth/authentication-state.service';
import { CsrfTokenStore } from '../auth/csrf-token.store';
import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { OrdersRealtimeService } from '../realtime/orders-realtime.service';
import { apiAuthInterceptor } from './api-auth.interceptor';

describe('apiAuthInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let csrf: CsrfTokenStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiAuthInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        AuthenticationState,
        CsrfTokenStore,
        {
          provide: APP_RUNTIME_CONFIG,
          useValue: {
            apiBaseUrl: 'https://api.example.com/base',
            signalRHubUrl: 'https://api.example.com/base/hubs/orders',
            pickupSignalRHubUrl: 'https://api.example.com/base/hubs/pickup',
          },
        },
        { provide: OrdersRealtimeService, useValue: { stop: () => Promise.resolve() } },
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    csrf = TestBed.inject(CsrfTokenStore);
  });

  afterEach(() => http.verify());

  it.each(['POST', 'PUT', 'PATCH', 'DELETE'])('adds credentials and csrf to API %s', (method) => {
    csrf.setToken('csrf-token');

    client.request(method, 'https://api.example.com/base/api/orders', { body: {} }).subscribe();

    const request = http.expectOne('https://api.example.com/base/api/orders');
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('csrf-token');
    request.flush({});
  });

  it('sends API GET with credentials but without csrf header', () => {
    csrf.setToken('csrf-token');

    client.get('https://api.example.com/base/api/orders').subscribe();

    const request = http.expectOne('https://api.example.com/base/api/orders');
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    request.flush([]);
  });

  it('does not alter external requests or lookalike origins', () => {
    client.post('https://api.example.com.evil/base/api/orders', {}).subscribe();

    const request = http.expectOne('https://api.example.com.evil/base/api/orders');
    expect(request.request.withCredentials).toBe(false);
    expect(request.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    request.flush({});
  });
});
