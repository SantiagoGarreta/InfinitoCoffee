import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import { User } from '../models/user.model';
import { UsersApiService } from './users-api.service';

describe('UsersApiService', () => {
  let service: UsersApiService;
  let http: HttpTestingController;
  const baseUrl = 'https://api.test/base/api/users';
  const user: User = {
    id: 'user-1',
    username: 'cashier.one',
    displayName: 'Caja Uno',
    role: 'Cashier',
    isActive: true,
    isSystemUser: false,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        UsersApiService,
        {
          provide: APP_RUNTIME_CONFIG,
          useValue: {
            apiBaseUrl: 'https://api.test/base',
            signalRHubUrl: 'https://api.test/base/hubs/orders',
            pickupSignalRHubUrl: 'https://api.test/base/hubs/pickup',
          },
        },
      ],
    });
    service = TestBed.inject(UsersApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets the complete user collection', async () => {
    const result = service.getUsers();
    const request = http.expectOne(baseUrl);
    expect(request.request.method).toBe('GET');
    request.flush([user]);
    await expect(result).resolves.toEqual([user]);
  });

  it('creates a user with the exact request body', async () => {
    const body = {
      username: 'cashier.one',
      displayName: 'Caja Uno',
      password: ' password preserved ',
      role: 'Cashier' as const,
    };
    const result = service.createUser(body);
    const request = http.expectOne(baseUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(user);
    await expect(result).resolves.toEqual(user);
  });

  it('updates a user with PUT and maps the response', async () => {
    const body = { username: 'kitchen.one', displayName: 'Cocina Uno', role: 'Kitchen' as const };
    const updated = { ...user, ...body };
    const result = service.updateUser(user.id, body);
    const request = http.expectOne(`${baseUrl}/${user.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(body);
    request.flush(updated);
    await expect(result).resolves.toEqual(updated);
  });

  it.each([
    ['activateUser', 'activate', true],
    ['deactivateUser', 'deactivate', false],
  ] as const)('uses the %s endpoint and maps its response', async (method, action, isActive) => {
    const result = service[method](user.id);
    const request = http.expectOne(`${baseUrl}/${user.id}/${action}`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({ ...user, isActive });
    await expect(result).resolves.toEqual({ ...user, isActive });
  });

  it('posts the exact password reset body and accepts 204', async () => {
    const body = { newPassword: ' new password preserved ' };
    const result = service.resetPassword(user.id, body);
    const request = http.expectOne(`${baseUrl}/${user.id}/reset-password`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(null, { status: 204, statusText: 'No Content' });
    await expect(result).resolves.toBeUndefined();
  });
});
