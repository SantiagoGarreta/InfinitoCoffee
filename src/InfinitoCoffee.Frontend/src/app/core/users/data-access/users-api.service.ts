import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../../config/app-runtime-config';
import {
  CreateUserRequest,
  ResetUserPasswordRequest,
  UpdateUserRequest,
} from '../models/user-requests.model';
import { User } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly runtimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly usersBaseUrl = `${this.runtimeConfig.apiBaseUrl}/api/users`;

  getUsers(): Promise<User[]> {
    return firstValueFrom(this.httpClient.get<User[]>(this.usersBaseUrl));
  }

  createUser(request: CreateUserRequest): Promise<User> {
    return firstValueFrom(this.httpClient.post<User>(this.usersBaseUrl, request));
  }

  updateUser(id: string, request: UpdateUserRequest): Promise<User> {
    return firstValueFrom(this.httpClient.put<User>(`${this.usersBaseUrl}/${id}`, request));
  }

  activateUser(id: string): Promise<User> {
    return firstValueFrom(this.httpClient.post<User>(`${this.usersBaseUrl}/${id}/activate`, null));
  }

  deactivateUser(id: string): Promise<User> {
    return firstValueFrom(this.httpClient.post<User>(`${this.usersBaseUrl}/${id}/deactivate`, null));
  }

  resetPassword(id: string, request: ResetUserPasswordRequest): Promise<void> {
    return firstValueFrom(
      this.httpClient.post<void>(`${this.usersBaseUrl}/${id}/reset-password`, request),
    ).then(() => undefined);
  }
}
