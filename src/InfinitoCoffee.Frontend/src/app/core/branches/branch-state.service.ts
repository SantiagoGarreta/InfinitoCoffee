import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { AuthenticationState } from '../auth/authentication-state.service';

export interface Branch { id: number; name: string }

@Injectable({ providedIn: 'root' })
export class BranchState {
  private readonly auth = inject(AuthenticationState);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly branches = signal<Branch[]>([{ id: 1, name: 'Sucursal 1' }, { id: 2, name: 'Sucursal 2' }]).asReadonly();
  private readonly selected = signal(this.readSelected());
  readonly privateId = computed(() => this.auth.currentUser()?.role === 'Administrator'
    ? this.selected() : this.auth.currentUser()?.branchId ?? 1);
  readonly publicId = signal(this.readPublic());
  readonly isAdministrator = computed(() => this.auth.currentUser()?.role === 'Administrator');

  name(id: number): string { return this.branches().find(x => x.id === id)?.name ?? `Sucursal ${id}`; }
  selectPrivate(id: number): void {
    if (!this.isAdministrator() || !this.branches().some(x => x.id === id)) return;
    this.selected.set(id);
    if (this.isBrowser) {
      try { sessionStorage.setItem('infinito.branch', String(id)); } catch { /* Restricted browser storage. */ }
    }
  }
  hubUrl(url: string, publicPickup = false): string {
    return `${url}${url.includes('?') ? '&' : '?'}branchId=${publicPickup ? this.publicId() : this.privateId()}`;
  }
  private readSelected(): number {
    if (!this.isBrowser) return 1;
    try { return Number(sessionStorage.getItem('infinito.branch')) === 2 ? 2 : 1; } catch { return 1; }
  }
  private readPublic(): number {
    if (!this.isBrowser) return 1;
    const value = new URLSearchParams(window.location.search).get('branchId');
    // Keep invalid explicit values so the API rejects them instead of displaying another branch.
    return value === null ? 1 : Number(value);
  }
}
