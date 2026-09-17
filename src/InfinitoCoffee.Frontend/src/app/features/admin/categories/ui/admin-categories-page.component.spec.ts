import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ProductCategory } from '../../../../core/product-categories/models/product-category.model';
import { AdminCategoriesStore } from '../data-access/admin-categories.store';
import { AdminCategoriesPageComponent } from './admin-categories-page.component';

class FakeAdminCategoriesStore {
  readonly categories = signal<ProductCategory[]>([
    { id: 'c-1', name: 'Cafés', isActive: true },
    { id: 'c-2', name: 'Tés', isActive: false },
  ]);
  readonly sortedCategories = signal(this.categories());
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly mutationError = signal<string | null>(null);
  readonly successMessage = signal<string | null>('Categorías listas.');
  loadCalls = 0;
  load(): Promise<void> { this.loadCalls++; return Promise.resolve(); }
  clearFeedback(): void { this.mutationError.set(null); this.successMessage.set(null); }
  create(): Promise<ProductCategory | null> { return Promise.resolve(this.categories()[0]!); }
  update(): Promise<ProductCategory | null> { return Promise.resolve(this.categories()[0]!); }
  activate(): Promise<ProductCategory | null> { return Promise.resolve(this.categories()[1]!); }
  deactivate(): Promise<ProductCategory | null> { return Promise.resolve(this.categories()[0]!); }
}

describe('AdminCategoriesPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminCategoriesPageComponent],
      providers: [{ provide: AdminCategoriesStore, useClass: FakeAdminCategoriesStore }],
    }).compileComponents();
  });

  it('lists active and inactive categories without delete', async () => {
    const fixture = TestBed.createComponent(AdminCategoriesPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Cafés');
    expect(text).toContain('Activa');
    expect(text).toContain('Tés');
    expect(text).toContain('Inactiva');
    expect(text).not.toContain('Eliminar');
  });

  it('shows the approved inline explanation before deactivation', async () => {
    const fixture = TestBed.createComponent(AdminCategoriesPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.askToDeactivate('c-1');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Los productos conservarán su estado');
    expect(fixture.nativeElement.textContent).toContain('no aparecerán en Caja');
    expect(fixture.nativeElement.textContent).toContain('Confirmar');
    expect(fixture.nativeElement.textContent).toContain('Cancelar');
  });

  it('requires a non-empty name no longer than 150 characters', async () => {
    const fixture = TestBed.createComponent(AdminCategoriesPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.name.set('');
    expect(fixture.componentInstance.canSubmit()).toBe(false);
    fixture.componentInstance.name.set('x'.repeat(151));
    expect(fixture.componentInstance.canSubmit()).toBe(false);
    fixture.componentInstance.name.set('Fríos');
    expect(fixture.componentInstance.canSubmit()).toBe(true);
  });
});
