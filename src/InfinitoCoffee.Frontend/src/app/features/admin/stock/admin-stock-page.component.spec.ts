import { TestBed } from '@angular/core/testing';
import { ProductsApiService } from '../../../core/products/data-access/products-api.service';
import { AdminStockPageComponent } from './admin-stock-page.component';
import { StockApiService, StockDashboard } from './stock-api.service';

const dashboard: StockDashboard = {
  items: [
    { id: 'eggs', name: 'Huevos', kind: 'Ingredient', unit: 'Unit', minimumQuantity: 5 },
    { id: 'scones', name: 'Scones', kind: 'FinishedProduct', unit: 'Unit', productId: 'product', minimumQuantity: 0 },
  ],
  balances: [
    { itemId: 'eggs', location: 'Factory', quantity: 20, revision: 'one' },
    { itemId: 'scones', location: 'Factory', quantity: 10, revision: 'two' },
  ],
  recipes: [{ id: 'recipe', itemId: 'scones', version: 1, yield: 10, createdAtUtc: '', lines: [{ itemId: 'eggs', quantity: 5 }] }],
};

describe('AdminStockPageComponent', () => {
  let api: { dashboard: ReturnType<typeof vi.fn>; history: ReturnType<typeof vi.fn>; record: ReturnType<typeof vi.fn>;
    saveRecipe: ReturnType<typeof vi.fn>; createItem: ReturnType<typeof vi.fn>; updateItem: ReturnType<typeof vi.fn> };
  beforeEach(async () => {
    api = { dashboard: vi.fn().mockResolvedValue(structuredClone(dashboard)),
      history: vi.fn().mockResolvedValue({ items: [], hasMore: false }), record: vi.fn().mockResolvedValue(undefined),
      saveRecipe: vi.fn().mockResolvedValue(undefined), createItem: vi.fn(), updateItem: vi.fn() };
    await TestBed.configureTestingModule({ imports: [AdminStockPageComponent], providers: [
      { provide: StockApiService, useValue: api }, { provide: ProductsApiService, useValue: { getProducts: () => Promise.resolve([]) } },
    ] }).compileComponents();
  });
  async function create() {
    const fixture = TestBed.createComponent(AdminStockPageComponent);
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance.loading()).toBe(false));
    await fixture.whenStable(); fixture.detectChanges(); return fixture;
  }
  it('shows a single general quantity without stations or transfers', async () => {
    const fixture = await create();
    expect(fixture.nativeElement.textContent).toContain('Ingredientes compartidos');
    expect(fixture.nativeElement.textContent).toContain('Huevos');
    expect(fixture.nativeElement.textContent).not.toContain('En tránsito');
    expect(fixture.nativeElement.textContent).not.toContain('Confirmar recepción');
  });
  it('filters and highlights items below their minimum, then shows all items again', async () => {
    const lowDashboard = structuredClone(dashboard);
    lowDashboard.balances[0].quantity = 2;
    api.dashboard.mockResolvedValue(lowDashboard);
    const fixture = await create();
    const rows = () => fixture.nativeElement.querySelectorAll('tbody tr') as NodeListOf<HTMLTableRowElement>;
    const filter = () => fixture.nativeElement.querySelector('.low-stock-filter') as HTMLButtonElement;

    expect(rows()).toHaveLength(2);
    expect(rows()[0].classList.contains('low-stock-row')).toBe(true);
    expect(rows()[0].textContent).toContain('Mínimo: 5 un.');
    expect(filter().textContent).toContain('Bajo mínimo (1)');

    filter().click(); fixture.detectChanges();
    expect(filter().getAttribute('aria-pressed')).toBe('true');
    expect(rows()).toHaveLength(1);
    expect(rows()[0].textContent).toContain('Huevos');

    fixture.componentInstance.search.set('scones'); fixture.detectChanges();
    expect(rows()).toHaveLength(1);
    expect(rows()[0].textContent).toContain('No hay artículos bajo el mínimo');

    fixture.componentInstance.search.set('');
    filter().click(); fixture.detectChanges();
    expect(filter().getAttribute('aria-pressed')).toBe('false');
    expect(rows()).toHaveLength(2);
  });
  it('records a decrease of a finished product with a reason', async () => {
    const fixture = await create(); const component = fixture.componentInstance;
    component.openAdjustment('scones', 'decrease');
    component.adjustmentQuantity = 2; component.notes = 'Dos descartados';
    expect(component.canAdjust).toBe(true);
    await component.adjust();
    expect(api.record).toHaveBeenCalledWith('adjustments', {
      notes: 'Dos descartados', consumeIngredients: true, lines: [{ itemId: 'scones', delta: -2, unit: 'unit' }],
    });
  });
  it('blocks a decrease larger than the available amount', async () => {
    const fixture = await create(); const component = fixture.componentInstance;
    component.openAdjustment('scones', 'decrease');
    component.adjustmentQuantity = 11; component.notes = 'Descarte';
    expect(component.canAdjust).toBe(false);
  });
  it('previews recipe ingredients when receiving finished products', async () => {
    const fixture = await create(); const component = fixture.componentInstance;
    component.openAdjustment('scones', 'increase');
    component.adjustmentQuantity = 10; component.notes = 'Llegaron diez scones';
    fixture.detectChanges();
    expect(component.ingredientPreview()).toEqual([{ item: dashboard.items[0], needed: 5, available: 20 }]);
    expect(fixture.nativeElement.textContent).toContain('Ingredientes compartidos que se descontarán');
    expect(component.canAdjust).toBe(true);
    component.adjustmentQuantity = 50;
    expect(component.canAdjust).toBe(false);
  });
  it('allows a transfer receipt without consuming ingredients or blocking on their availability', async () => {
    const fixture = await create(); const component = fixture.componentInstance;
    component.openAdjustment('scones', 'increase');
    component.adjustmentQuantity = 50; component.notes = 'Traslado de sucursal 2';
    expect(component.canAdjust).toBe(false);
    component.consumeIngredients = false;
    expect(component.canAdjust).toBe(true);
    expect(component.ingredientPreview()).toEqual([]);
    await component.adjust();
    expect(api.record).toHaveBeenCalledWith('adjustments', {
      notes: 'Traslado de sucursal 2', consumeIngredients: false,
      lines: [{ itemId: 'scones', delta: 50, unit: 'unit' }],
    });
  });
});
