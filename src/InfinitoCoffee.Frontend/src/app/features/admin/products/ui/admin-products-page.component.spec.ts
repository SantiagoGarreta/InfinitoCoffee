import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Product } from '../../../../core/products/models/product.model';
import { AdminProductsStore } from '../data-access/admin-products.store';
import { AdminProductsPageComponent } from './admin-products-page.component';

class FakeAdminProductsStore {
  readonly products = signal<Product[]>([
    { id: 'p-1', name: 'Espresso', description: 'Corto', price: 8, categoryId: 'c-1', isActive: true },
    { id: 'p-2', name: 'Latte', description: null, price: 10, categoryId: 'c-2', isActive: true },
    { id: 'p-3', name: 'Mocha', description: null, price: 12, categoryId: 'c-1', isActive: false },
  ]);
  readonly categories = signal([
    { id: 'c-1', name: 'Cafés', isActive: true },
    { id: 'c-2', name: 'Temporales', isActive: false },
    { id: 'c-3', name: 'Oculta', isActive: false },
  ]);
  readonly activeCategories = signal([this.categories()[0]!]);
  readonly sortedProducts = signal(this.products());
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly mutationError = signal<string | null>(null);
  readonly successMessage = signal<string | null>('Catálogo listo.');
  loadCalls = 0;
  load(): Promise<void> { this.loadCalls++; return Promise.resolve(); }
  clearFeedback(): void { this.mutationError.set(null); this.successMessage.set(null); }
  categoryFor(id: string) { return this.categories().find((category) => category.id === id); }
  create(): Promise<Product | null> { return Promise.resolve(this.products()[0]!); }
  update(): Promise<Product | null> { return Promise.resolve(this.products()[0]!); }
  activate(): Promise<Product | null> { return Promise.resolve(this.products()[0]!); }
  deactivate(): Promise<Product | null> { return Promise.resolve(this.products()[0]!); }
}

describe('AdminProductsPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminProductsPageComponent],
      providers: [provideRouter([]), { provide: AdminProductsStore, useClass: FakeAdminProductsStore }],
    }).compileComponents();
  });

  it('shows active, unavailable and inactive states without a delete action', async () => {
    const fixture = TestBed.createComponent(AdminProductsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Disponible en Caja');
    expect(text).toContain('No disponible: categoría inactiva');
    expect(text).toContain('Inactivo');
    expect(text).not.toContain('Eliminar');
  });

  it('keeps the current inactive category while excluding other inactive destinations', async () => {
    const fixture = TestBed.createComponent(AdminProductsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance;
    component.startEdit(component.store.products()[1]!);
    fixture.detectChanges();
    const options = [...fixture.nativeElement.querySelectorAll('option')].map((option: HTMLOptionElement) => option.textContent);
    expect(options.some((option) => option?.includes('Temporales'))).toBe(true);
    expect(options.some((option) => option?.includes('Oculta'))).toBe(false);
  });

  it('uses inline confirmation before deactivation', async () => {
    const fixture = TestBed.createComponent(AdminProductsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.askToDeactivate('p-1');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.inline-confirmation')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Confirmar');
    expect(fixture.nativeElement.textContent).toContain('Cancelar');
  });

  it('disables create when there are no active categories', async () => {
    const fixture = TestBed.createComponent(AdminProductsPageComponent);
    const store = TestBed.inject(AdminProductsStore) as unknown as FakeAdminProductsStore;
    store.activeCategories.set([]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Necesitás una categoría activa');
    expect((fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
  });
});
