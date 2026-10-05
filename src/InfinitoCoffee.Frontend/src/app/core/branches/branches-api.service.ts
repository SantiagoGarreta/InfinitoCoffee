import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { Branch } from './branch-state.service';

@Injectable({ providedIn: 'root' })
export class BranchesApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_RUNTIME_CONFIG).apiBaseUrl}/api/branches`;
  getAll(): Promise<Branch[]> { return firstValueFrom(this.http.get<Branch[]>(this.url)); }
  rename(id: number, name: string): Promise<Branch> {
    return firstValueFrom(this.http.put<Branch>(`${this.url}/${id}`, { name }));
  }
}
