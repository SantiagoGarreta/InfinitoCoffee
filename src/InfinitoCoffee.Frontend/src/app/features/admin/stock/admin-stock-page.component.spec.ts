import { TestBed } from '@angular/core/testing';
import { ProductsApiService } from '../../../core/products/data-access/products-api.service';
import { AdminStockPageComponent } from './admin-stock-page.component';
import { StockApiService, StockDashboard, Transfer } from './stock-api.service';

const initial: StockDashboard = {
  items: [
    { id: 'eggs', name: 'Huevos', kind: 'Ingredient', unit: 'Unit', minimumQuantity: 5 },
    { id: 'scones', name: 'Scones', kind: 'FinishedProduct', unit: 'Unit', productId: 'product', minimumQuantity: 0 },
  ],
  balances: [
    { itemId: 'eggs', location: 'Factory', quantity: 20, revision: 'revision-original' },
    { itemId: 'scones', location: 'Factory', quantity: 20, revision: 'scones-original' },
    { itemId: 'eggs', location: 'Cafe', quantity: 5, revision: 'revision-cafe' },
  ],
  recipes: [{ id: 'recipe', itemId: 'scones', version: 1, yield: 10, createdAtUtc: '', lines: [{ itemId: 'eggs', quantity: 5 }] }],
};
const transfer: Transfer = { id: 'transfer', from: 'Factory', to: 'Cafe', sentAtUtc: '2026-09-24T10:00:00Z', lines: [{ itemId: 'scones', sent: 20 }] };

describe('AdminStockPageComponent', () => {
  let api: { dashboard: ReturnType<typeof vi.fn>; transfers: ReturnType<typeof vi.fn>; history: ReturnType<typeof vi.fn>;
    record: ReturnType<typeof vi.fn>; saveRecipe: ReturnType<typeof vi.fn>; createItem: ReturnType<typeof vi.fn>; updateItem: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    api = { dashboard: vi.fn().mockResolvedValue(structuredClone(initial)), transfers: vi.fn().mockResolvedValue([transfer]),
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
    await fixture.whenStable(); fixture.detectChanges();
    return fixture;
  }

  it('shows stock by location and one-click access to pending receipts', async () => {
    const fixture = await create();
    expect(fixture.nativeElement.textContent).toContain('Huevos');
    expect(fixture.nativeElement.textContent).toContain('Existencias por ubicación');
    expect(fixture.nativeElement.textContent).toContain('Confirmar recepción');
  });

  it('scales the recipe for 20 scones and prevents production without ingredients', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.openAction('production'); page.productionRecipeId = 'recipe'; page.productionQuantity = 20;
    expect(page.productionPreview()[0].needed).toBe(10);
    expect(page.canRecord).toBe(true);
    page.productionQuantity = 50;
    expect(page.canRecord).toBe(false);
    page.productionQuantity = 1;
    expect(page.canRecord).toBe(false); // Half an egg must not silently be rounded.
  });

  it('sends a production once and clears the form only after success', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.openAction('production'); page.productionRecipeId = 'recipe'; page.productionQuantity = 20;
    await page.record();
    expect(api.record).toHaveBeenCalledWith('production', { recipeId: 'recipe', quantity: 20, discardedQuantity: 0, location: 'Factory', notes: '' });
    expect(page.productionQuantity).toBeNull();
    expect(page.success()).toContain('Movimiento registrado');
  });

  it('keeps entered values after a failed save', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    api.record.mockRejectedValueOnce(new Error('offline'));
    page.openAction('receipts', 'eggs'); page.lines[0].quantity = 20;
    await page.record();
    expect(page.lines[0].quantity).toBe(20);
    expect(page.error()).toBeTruthy();
  });

  it('preserves the revision used when counting even if a newer balance arrives', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.openAction('counts', 'eggs'); page.lines[0].quantity = 18; page.notes = 'Conteo de cierre';
    page.data.set({ ...page.data(), balances: [{ itemId: 'eggs', location: 'Factory', quantity: 25, revision: 'changed' }] });
    expect(page.countDifference(page.lines[0])).toBe(-2);
    await page.record();
    expect(api.record).toHaveBeenCalledWith('counts', expect.objectContaining({
      lines: [{ itemId: 'eggs', quantity: 18, unit: 'unit', expectedRevision: 'revision-original' }],
    }));
  });

  it('requires a reason for discrepancies and clears a count when changing location', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.openAction('counts', 'eggs'); page.lines[0].quantity = 18;
    expect(page.canRecord).toBe(false);
    page.notes = 'Cierre'; expect(page.canRecord).toBe(true);
    page.location = 'Cafe'; page.changeLocation();
    expect(page.lines[0].quantity).toBeNull(); expect(page.canRecord).toBe(false);
  });

  it('prefills a transfer receipt and requires explanation for missing products', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.beginReceive(transfer);
    expect(page.receiveLines[0].quantity).toBe(20); expect(page.canReceive).toBe(true);
    page.receiveLines[0].quantity = 18; expect(page.canReceive).toBe(false);
    page.receiveNotes = 'Dos dañados'; expect(page.canReceive).toBe(true);
    await page.receive();
    expect(api.record).toHaveBeenCalledWith('transfers/transfer/receive', { notes: 'Dos dañados', lines: [{ itemId: 'scones', quantity: 18, unit: 'unit' }] });
  });

  it('accepts integer inputs in the actual receipt and recipe forms', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.openAction('receipts', 'eggs'); page.lines[0].quantity = 20;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('.line-editor input[type="number"]') as HTMLInputElement;
    expect(input.validity.stepMismatch).toBe(false);
    expect(input.validity.valid).toBe(true);
    page.setTab('recipes'); page.recipeItemId = 'scones'; page.selectRecipeItem();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('.recipe-line input[type="number"]') as HTMLInputElement).validity.valid).toBe(true);
  });

  it('blocks repeated lines and transfers to the same location', async () => {
    const fixture = await create(); const page = fixture.componentInstance;
    page.openAction('receipts', 'eggs'); page.lines[0].quantity = 10;
    page.lines.push({ ...page.lines[0] }); expect(page.canRecord).toBe(false);
    page.openAction('transfers', 'eggs'); page.lines[0].quantity = 10; page.destination = 'Factory';
    expect(page.canRecord).toBe(false);
  });
});
