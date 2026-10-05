import { TestBed } from '@angular/core/testing';
import { AuthenticationState } from '../auth/authentication-state.service';
import { BranchState } from './branch-state.service';

describe('BranchState', () => {
  beforeEach(() => { sessionStorage.clear(); window.history.replaceState(null, '', '/'); });
  afterEach(() => { sessionStorage.clear(); window.history.replaceState(null, '', '/'); });

  it('keeps a staff branch fixed and permits administrators to select another branch', () => {
    const auth = TestBed.inject(AuthenticationState);
    const state = TestBed.inject(BranchState);
    auth.setUser({ id: '1', username: 'cocina', displayName: 'Cocina', role: 'Kitchen', branchId: 2 });
    state.selectPrivate(1);
    expect(state.privateId()).toBe(2);
    expect(state.hubUrl('https://api.test/hubs/orders')).toContain('branchId=2');
    auth.setUser({ id: '2', username: 'admin', displayName: 'Admin', role: 'Administrator', branchId: 1 });
    state.selectPrivate(2);
    expect(state.privateId()).toBe(2);
    expect(sessionStorage.getItem('infinito.branch')).toBe('2');
  });

  it('uses the pickup URL independently of the private staff branch', () => {
    window.history.replaceState(null, '', '/pickup?branchId=2');
    TestBed.inject(AuthenticationState).setUser({ id: '1', username: 'cocina', displayName: 'Cocina', role: 'Kitchen', branchId: 1 });
    const state = TestBed.inject(BranchState);
    expect(state.privateId()).toBe(1);
    expect(state.hubUrl('https://api.test/hubs/pickup', true)).toBe('https://api.test/hubs/pickup?branchId=2');
  });
});
