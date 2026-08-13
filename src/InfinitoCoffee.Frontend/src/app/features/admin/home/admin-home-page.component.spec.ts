import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthenticationState } from '../../../core/auth/authentication-state.service';
import { AdminHomePageComponent } from './admin-home-page.component';

describe('AdminHomePageComponent', () => {
  it('renders the welcome and six real navigation accesses without invented metrics', async () => {
    await TestBed.configureTestingModule({
      imports: [AdminHomePageComponent],
      providers: [AuthenticationState, provideRouter([])],
    }).compileComponents();
    TestBed.inject(AuthenticationState).setUser({ id: '1', username: 'admin', displayName: 'Ana Admin', role: 'Administrator' });
    const fixture = TestBed.createComponent(AdminHomePageComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    const links = fixture.nativeElement.querySelectorAll('.admin-card') as NodeListOf<HTMLAnchorElement>;

    expect(text).toContain('Administración');
    expect(text).toContain('Ana Admin');
    expect(links).toHaveLength(6);
    ['Caja', 'Cocina', 'Pickup', 'Productos', 'Categorías', 'Usuarios'].forEach((label) => expect(text).toContain(label));
    expect(text).not.toMatch(/ventas|métrica|estadística|gráfico/i);
    const pickup = [...links].find((link) => link.textContent?.includes('Pickup'))!;
    expect(pickup.getAttribute('target')).toBe('_blank');
    expect(pickup.getAttribute('rel')).toContain('noopener');
  });
});
