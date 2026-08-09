import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class CsrfTokenStore {
  private readonly token = signal<string | null>(null);

  getToken(): string | null {
    return this.token();
  }

  setToken(token: string): void {
    this.token.set(token);
  }

  clear(): void {
    this.token.set(null);
  }
}
