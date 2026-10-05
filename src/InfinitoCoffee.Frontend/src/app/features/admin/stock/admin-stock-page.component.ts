import { Component, LOCALE_ID, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, isPlatformBrowser, registerLocaleData } from '@angular/common';
import localeUruguay from '@angular/common/locales/es-UY';
import { FormsModule } from '@angular/forms';
import { BranchState } from '../../../core/branches/branch-state.service';
import { ProductsApiService } from '../../../core/products/data-access/products-api.service';
import { Product } from '../../../core/products/models/product.model';
import { toUserMessage } from '../../../core/http/api-error.utils';
import { History, LineInput, StockApiService, StockDashboard, StockItem, Unit } from './stock-api.service';

type Tab = 'overview' | 'adjust' | 'recipes' | 'items' | 'history';
interface DraftLine { itemId: string; quantity: number | null; unit: string }
registerLocaleData(localeUruguay);

@Component({ selector: 'app-admin-stock-page', standalone: true,
  imports: [FormsModule, DecimalPipe, DatePipe], providers: [{ provide: LOCALE_ID, useValue: 'es-UY' }],
  templateUrl: './admin-stock-page.component.html', styleUrl: './admin-stock-page.component.scss' })
export class AdminStockPageComponent implements OnInit {
  readonly branch = inject(BranchState);
  private readonly api = inject(StockApiService);
  private readonly productsApi = inject(ProductsApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly data = signal<StockDashboard>({ items: [], balances: [], recipes: [] });
  readonly products = signal<Product[]>([]);
  readonly history = signal<History>({ items: [], hasMore: false });
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly tab = signal<Tab>('overview');
  readonly search = signal('');
  readonly ingredients = computed(() => this.data().items.filter(x => x.kind === 'Ingredient'));
  readonly finished = computed(() => this.data().items.filter(x => x.kind === 'FinishedProduct'));
  readonly filteredItems = computed(() => this.data().items.filter(x => x.name.toLocaleLowerCase().includes(this.search().toLocaleLowerCase())));
  readonly lowStock = computed(() => this.data().items.filter(x => x.minimumQuantity > 0 && this.quantity(x.id) < x.minimumQuantity));
  readonly availableProducts = computed(() => this.products().filter(p => !this.data().items.some(x => x.productId === p.id)));
  readonly tabs: { id: Tab; label: string }[] = [
    { id: 'overview', label: 'Existencias' }, { id: 'adjust', label: 'Ajustar stock' },
    { id: 'recipes', label: 'Recetas' }, { id: 'items', label: 'Artículos' }, { id: 'history', label: 'Historial' } ];
  adjustmentItemId = '';
  adjustmentDirection: 'increase' | 'decrease' = 'increase';
  adjustmentQuantity: number | null = null;
  adjustmentUnit = 'unit';
  consumeIngredients = true;
  notes = '';
  itemId = '';
  itemName = '';
  itemKind: StockItem['kind'] = 'Ingredient';
  itemUnit: Unit = 'Unit';
  itemProductId = '';
  itemMinimum: number | null = 0;
  recipeItemId = '';
  recipeYield: number | null = 10;
  recipeLines: DraftLine[] = [this.blankLine()];
  historyPage = 0;
  historyItemId = '';
  private historyRequest = 0;

  ngOnInit(): void { if (this.isBrowser) void this.reload(); else this.loading.set(false); }
  async reload(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try { await this.fetchData(); }
    catch (error) { this.error.set(toUserMessage(error, 'No fue posible cargar el stock.')); }
    finally { this.loading.set(false); }
  }
  private async fetchData(): Promise<void> {
    const [data, products] = await Promise.all([this.api.dashboard(), this.productsApi.getProducts()]);
    this.data.set(data); this.products.set(products);
  }
  setTab(tab: Tab): void { this.tab.set(tab); this.error.set(null); if (tab === 'history') void this.loadHistory(0); }
  openAdjustment(itemId = '', direction: 'increase' | 'decrease' = 'increase'): void {
    this.adjustmentItemId = itemId; this.adjustmentDirection = direction;
    this.adjustmentQuantity = null; this.notes = ''; this.changeAdjustmentItem(); this.setTab('adjust');
  }
  blankLine(): DraftLine { return { itemId: '', quantity: null, unit: 'unit' }; }
  item(id: string): StockItem | undefined { return this.data().items.find(x => x.id === id); }
  itemLabel(id: string): string { return this.item(id)?.name ?? 'Artículo'; }
  quantity(id: string): number { return this.data().balances.find(x => x.itemId === id)?.quantity ?? 0; }
  unitLabel(unit: Unit): string { return unit === 'Unit' ? 'un.' : unit === 'Gram' ? 'g' : 'ml'; }
  baseUnit(unit: Unit): string { return unit === 'Unit' ? 'unit' : unit === 'Gram' ? 'g' : 'ml'; }
  unitOptions(itemId: string): { value: string; label: string }[] {
    const unit = this.item(itemId)?.unit;
    return unit === 'Gram' ? [{ value: 'g', label: 'g' }, { value: 'kg', label: 'kg' }]
      : unit === 'Milliliter' ? [{ value: 'ml', label: 'ml' }, { value: 'l', label: 'litros' }]
        : [{ value: 'unit', label: 'unidades' }];
  }
  changeAdjustmentItem(): void { this.consumeIngredients = true; this.adjustmentUnit = this.baseUnit(this.item(this.adjustmentItemId)?.unit ?? 'Unit'); }
  changeLineItem(line: DraftLine): void { line.unit = this.baseUnit(this.item(line.itemId)?.unit ?? 'Unit'); }
  adjustmentRecipe() { return this.data().recipes.find(x => x.itemId === this.adjustmentItemId); }
  ingredientPreview(): { item: StockItem; needed: number; available: number }[] {
    if (!this.consumeIngredients || this.adjustmentDirection !== 'increase' || this.item(this.adjustmentItemId)?.kind !== 'FinishedProduct'
      || !this.adjustmentQuantity || this.adjustmentQuantity <= 0) return [];
    const recipe = this.adjustmentRecipe();
    if (!recipe) return [];
    return recipe.lines.map(line => ({ item: this.item(line.itemId)!,
      needed: Math.round(line.quantity * this.adjustmentQuantity! / recipe.yield * 1000) / 1000,
      available: this.quantity(line.itemId) }));
  }
  get canAdjust(): boolean {
    const item = this.item(this.adjustmentItemId);
    const amount = (this.adjustmentQuantity ?? 0) * (this.adjustmentUnit === 'kg' || this.adjustmentUnit === 'l' ? 1000 : 1);
    return !!item && !!this.notes.trim() && this.adjustmentQuantity !== null && Number.isFinite(amount)
      && amount > 0 && (item.unit !== 'Unit' || Number.isInteger(amount))
      && (this.adjustmentDirection === 'increase' || amount <= this.quantity(item.id))
      && this.ingredientPreview().every(x => x.needed > 0 && x.needed <= x.available);
  }
  async adjust(): Promise<void> {
    if (!this.canAdjust || this.busy()) return;
    const delta = this.adjustmentDirection === 'increase' ? this.adjustmentQuantity! : -this.adjustmentQuantity!;
    await this.save(() => this.api.record('adjustments', { notes: this.notes.trim(), consumeIngredients: this.consumeIngredients, lines: [{ itemId: this.adjustmentItemId, delta, unit: this.adjustmentUnit }] }),
      'Stock actualizado.', () => { this.adjustmentQuantity = null; this.notes = ''; });
  }
  newItem(): void { this.itemId = ''; this.itemName = ''; this.itemProductId = ''; this.itemKind = 'Ingredient'; this.itemUnit = 'Unit'; this.itemMinimum = 0; }
  editItem(item: StockItem): void {
    this.itemId = item.id; this.itemName = item.name; this.itemKind = item.kind; this.itemUnit = item.unit;
    this.itemProductId = item.productId ?? ''; this.itemMinimum = item.minimumQuantity; this.setTab('items');
  }
  changeKind(): void { this.itemProductId = ''; if (this.itemKind === 'FinishedProduct') this.itemUnit = 'Unit'; }
  selectProduct(): void { this.itemName = this.products().find(x => x.id === this.itemProductId)?.name ?? ''; }
  get canSaveItem(): boolean { return !!this.itemName.trim() && this.itemMinimum !== null && this.itemMinimum >= 0 && (this.itemKind === 'Ingredient' || !!this.itemProductId); }
  async saveItem(): Promise<void> {
    if (!this.canSaveItem || this.busy()) return;
    await this.save(async () => {
      if (this.itemId) await this.api.updateItem(this.itemId, this.itemName.trim(), this.itemMinimum!);
      else await this.api.createItem({ name: this.itemName.trim(), kind: this.itemKind, unit: this.itemUnit,
        productId: this.itemProductId || null, minimumQuantity: this.itemMinimum! });
    }, 'Artículo guardado. Cargá la cantidad inicial desde Ajustar stock.', () => this.newItem());
  }
  selectRecipeItem(): void {
    const recipe = this.data().recipes.find(x => x.itemId === this.recipeItemId);
    this.recipeYield = recipe?.yield ?? 10;
    this.recipeLines = recipe ? recipe.lines.map(x => ({ itemId: x.itemId, quantity: x.quantity, unit: this.baseUnit(this.item(x.itemId)!.unit) })) : [this.blankLine()];
  }
  editRecipe(itemId: string): void { this.recipeItemId = itemId; this.selectRecipeItem(); this.setTab('recipes'); }
  get canSaveRecipe(): boolean {
    return !!this.recipeItemId && this.recipeYield !== null && this.recipeYield > 0 && Number.isInteger(this.recipeYield)
      && this.recipeLines.length > 0 && new Set(this.recipeLines.map(x => x.itemId)).size === this.recipeLines.length
      && this.recipeLines.every(x => x.quantity !== null && x.quantity > 0 && this.item(x.itemId)?.kind === 'Ingredient'
        && (this.item(x.itemId)?.unit !== 'Unit' || Number.isInteger(x.quantity)));
  }
  async saveRecipe(): Promise<void> {
    if (!this.canSaveRecipe || this.busy()) return;
    const lines: LineInput[] = this.recipeLines.map(x => ({ itemId: x.itemId, quantity: x.quantity!, unit: x.unit }));
    await this.save(async () => { await this.api.saveRecipe(this.recipeItemId, this.recipeYield!, lines); },
      'Receta guardada. Se usará al ingresar nuevas unidades de este producto.', () => { this.recipeItemId = ''; this.recipeLines = [this.blankLine()]; this.recipeYield = 10; });
  }
  async loadHistory(page: number): Promise<void> {
    const request = ++this.historyRequest; this.error.set(null);
    try { const history = await this.api.history(page, this.historyItemId);
      if (request === this.historyRequest) { this.history.set(history); this.historyPage = page; }
    } catch (error) { if (request === this.historyRequest) this.error.set(toUserMessage(error, 'No fue posible cargar el historial.')); }
  }
  operationLabel(type: string): string {
    return ({ Receipt: 'Ingreso', Production: 'Producción anterior', TransferSent: 'Envío anterior', TransferReceived: 'Recepción anterior',
      Waste: 'Merma anterior', Count: 'Conteo anterior', Adjustment: 'Ajuste manual', Sale: 'Pedido entregado' } as Record<string, string>)[type] ?? type;
  }
  private async save(action: () => Promise<void>, message: string, reset: () => void): Promise<void> {
    this.busy.set(true); this.error.set(null); this.success.set(null);
    try { await action(); reset(); this.success.set(message);
      try { await this.fetchData(); }
      catch { this.error.set('Se guardó la operación, pero no se pudo actualizar la vista. Usá Actualizar.'); }
    } catch (error) { this.error.set(toUserMessage(error, 'No fue posible guardar. Revisá los datos y reintentá.')); }
    finally { this.busy.set(false); }
  }
}
