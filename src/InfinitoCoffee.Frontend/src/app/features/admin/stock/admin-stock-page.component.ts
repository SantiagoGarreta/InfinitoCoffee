import { Component, LOCALE_ID, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, isPlatformBrowser, registerLocaleData } from '@angular/common';
import localeUruguay from '@angular/common/locales/es-UY';
import { FormsModule } from '@angular/forms';
import { ProductsApiService } from '../../../core/products/data-access/products-api.service';
import { Product } from '../../../core/products/models/product.model';
import { toUserMessage } from '../../../core/http/api-error.utils';
import { Balance, History, LineInput, Location, PhysicalLocation, Recipe, StockApiService, StockDashboard, StockItem, Transfer, Unit } from './stock-api.service';

type Tab = 'overview' | 'register' | 'recipes' | 'items' | 'history';
type Action = 'receipts' | 'production' | 'transfers' | 'counts' | 'waste';
registerLocaleData(localeUruguay);
interface DraftLine { itemId: string; quantity: number | null; unit: string; expectedRevision?: string; expectedQuantity?: number }

@Component({
  selector: 'app-admin-stock-page', standalone: true,
  imports: [FormsModule, DecimalPipe, DatePipe],
  providers: [{ provide: LOCALE_ID, useValue: 'es-UY' }],
  templateUrl: './admin-stock-page.component.html', styleUrl: './admin-stock-page.component.scss',
})
export class AdminStockPageComponent implements OnInit {
  private readonly api = inject(StockApiService);
  private readonly productsApi = inject(ProductsApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly data = signal<StockDashboard>({ items: [], balances: [], recipes: [] });
  readonly products = signal<Product[]>([]);
  readonly transfers = signal<Transfer[]>([]);
  readonly history = signal<History>({ items: [], hasMore: false });
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly tab = signal<Tab>('overview');
  readonly search = signal('');
  readonly pendingTransfers = computed(() => this.transfers().filter(x => !x.receivedAtUtc));
  readonly transferDifferences = computed(() => this.transfers().filter(x => x.receivedAtUtc && x.lines.some(l => l.received !== l.sent)));
  readonly ingredients = computed(() => this.data().items.filter(x => x.kind === 'Ingredient'));
  readonly finished = computed(() => this.data().items.filter(x => x.kind === 'FinishedProduct'));
  readonly filteredItems = computed(() => this.data().items.filter(x => x.name.toLocaleLowerCase().includes(this.search().toLocaleLowerCase())));
  readonly lowStock = computed(() => this.data().items.filter(x => x.minimumQuantity > 0
    && this.quantity(x.id, x.kind === 'Ingredient' ? 'Factory' : 'Cafe') < x.minimumQuantity));
  readonly availableProducts = computed(() => this.products().filter(p => !this.data().items.some(x => x.productId === p.id)));
  readonly tabs: { id: Tab; label: string }[] = [
    { id: 'overview', label: 'Existencias' }, { id: 'register', label: 'Registrar movimiento' },
    { id: 'recipes', label: 'Recetas' }, { id: 'items', label: 'Artículos' }, { id: 'history', label: 'Historial' },
  ];
  readonly actions: { id: Action; label: string }[] = [
    { id: 'receipts', label: 'Ingreso / compra' }, { id: 'production', label: 'Producción' },
    { id: 'transfers', label: 'Envío' }, { id: 'counts', label: 'Conteo físico' }, { id: 'waste', label: 'Merma' },
  ];
  readonly locations: { id: PhysicalLocation; label: string }[] = [{ id: 'Factory', label: 'Fábrica' }, { id: 'Cafe', label: 'Cafetería' }];
  action: Action = 'receipts';
  location: PhysicalLocation = 'Factory';
  destination: PhysicalLocation = 'Cafe';
  notes = '';
  lines: DraftLine[] = [this.blankLine()];
  productionRecipeId = '';
  productionQuantity: number | null = null;
  productionDiscarded: number | null = 0;
  receiveId = '';
  receiveLines: DraftLine[] = [];
  receiveNotes = '';
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
  historyLocation = '';
  private historyRequest = 0;

  ngOnInit(): void { if (this.isBrowser) void this.reload(); else this.loading.set(false); }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.fetchData();
      if (this.action === 'counts') this.lines = [this.blankLine()];
    }
    catch (error) { this.error.set(toUserMessage(error, 'No fue posible cargar el stock.')); }
    finally { this.loading.set(false); }
  }

  private async fetchData(): Promise<void> {
    const [data, transfers, products] = await Promise.all([this.api.dashboard(), this.api.transfers(), this.productsApi.getProducts()]);
    this.data.set(data);
    this.transfers.set(transfers);
    this.products.set(products);
  }

  setTab(tab: Tab): void {
    this.tab.set(tab);
    this.error.set(null);
    if (tab === 'history') void this.loadHistory(0);
  }

  openAction(action: Action, itemId = ''): void {
    this.action = action;
    this.notes = '';
    this.productionDiscarded = 0;
    this.lines = [this.blankLine()];
    if (itemId) { this.lines[0].itemId = itemId; this.changeLineItem(this.lines[0]); }
    this.receiveId = '';
    this.setTab('register');
  }

  blankLine(): DraftLine { return { itemId: '', quantity: null, unit: 'unit' }; }
  item(id: string): StockItem | undefined { return this.data().items.find(x => x.id === id); }
  itemLabel(id: string): string { return this.item(id)?.name ?? 'Artículo'; }
  balance(id: string, location: Location): Balance | undefined { return this.data().balances.find(x => x.itemId === id && x.location === location); }
  quantity(id: string, location: Location): number { return this.balance(id, location)?.quantity ?? 0; }
  locationLabel(location: Location): string { return location === 'Factory' ? 'Fábrica' : location === 'Cafe' ? 'Cafetería' : 'En tránsito'; }
  unitLabel(unit: Unit): string { return unit === 'Unit' ? 'un.' : unit === 'Gram' ? 'g' : 'ml'; }
  baseUnit(unit: Unit): string { return unit === 'Unit' ? 'unit' : unit === 'Gram' ? 'g' : 'ml'; }
  unitOptions(itemId: string): { value: string; label: string }[] {
    const unit = this.item(itemId)?.unit;
    return unit === 'Gram' ? [{ value: 'g', label: 'g' }, { value: 'kg', label: 'kg' }]
      : unit === 'Milliliter' ? [{ value: 'ml', label: 'ml' }, { value: 'l', label: 'litros' }]
        : [{ value: 'unit', label: 'unidades' }];
  }
  changeLineItem(line: DraftLine): void {
    line.unit = this.baseUnit(this.item(line.itemId)?.unit ?? 'Unit');
    const balance = this.balance(line.itemId, this.location);
    line.expectedRevision = balance?.revision;
    line.expectedQuantity = balance?.quantity;
  }
  changeLocation(): void {
    for (const line of this.lines) {
      this.changeLineItem(line);
      if (this.action === 'counts') line.quantity = null;
    }
  }
  baseQuantity(line: DraftLine): number { return (line.quantity ?? 0) * (line.unit === 'kg' || line.unit === 'l' ? 1000 : 1); }
  countDifference(line: DraftLine): number { return this.baseQuantity(line) - (line.expectedQuantity ?? 0); }

  productionRecipe(): Recipe | undefined { return this.data().recipes.find(x => x.id === this.productionRecipeId); }
  selectProductionRecipe(): void { this.productionQuantity = this.productionRecipe()?.yield ?? null; }
  productionPreview(): { name: string; needed: number; available: number; unit: string; invalid: boolean }[] {
    const recipe = this.productionRecipe();
    if (!recipe || !this.productionQuantity) return [];
    return recipe.lines.map(line => {
      const item = this.item(line.itemId)!;
      const needed = line.quantity * this.productionQuantity! / recipe.yield;
      return { name: item.name, needed, available: this.quantity(item.id, this.location), unit: this.unitLabel(item.unit),
        invalid: item.unit === 'Unit' ? !Number.isInteger(needed) : Math.abs(needed * 1000 - Math.round(needed * 1000)) > 0.000001 };
    });
  }

  get canRecord(): boolean {
    if (this.action === 'production') return !!this.productionRecipe() && this.productionQuantity !== null
      && this.productionQuantity > 0 && Number.isInteger(this.productionQuantity)
      && this.productionDiscarded !== null && this.productionDiscarded >= 0 && Number.isInteger(this.productionDiscarded)
      && this.productionDiscarded <= this.productionQuantity && (this.productionDiscarded === 0 || !!this.notes.trim())
      && this.productionPreview().every(x => !x.invalid && x.needed <= x.available);
    if (!this.validLines(this.lines, this.action === 'counts')) return false;
    if ((this.action === 'counts' || this.action === 'waste') && !this.notes.trim()) return false;
    if (this.action === 'counts' && this.lines.some(x => !x.expectedRevision)) return false;
    if (this.action === 'transfers' && this.location === this.destination) return false;
    if (this.action === 'transfers' || this.action === 'waste')
      return this.lines.every(x => this.baseQuantity(x) <= this.quantity(x.itemId, this.location));
    return true;
  }

  private validLines(lines: DraftLine[], allowZero = false): boolean {
    return lines.length > 0 && lines.length <= 100 && new Set(lines.map(x => x.itemId)).size === lines.length
      && lines.every(x => !!this.item(x.itemId) && x.quantity !== null && Number.isFinite(x.quantity)
        && (allowZero ? x.quantity >= 0 : x.quantity > 0)
        && (this.item(x.itemId)?.unit !== 'Unit' || Number.isInteger(x.quantity)));
  }
  private inputs(lines: DraftLine[]): LineInput[] { return lines.map(x => ({ itemId: x.itemId, quantity: x.quantity!, unit: x.unit })); }

  async record(): Promise<void> {
    if (!this.canRecord || this.busy()) return;
    const notes = this.notes.trim();
    let body: object;
    if (this.action === 'production') body = { recipeId: this.productionRecipeId, quantity: this.productionQuantity, discardedQuantity: this.productionDiscarded, location: this.location, notes };
    else if (this.action === 'transfers') body = { from: this.location, to: this.destination, notes, lines: this.inputs(this.lines) };
    else if (this.action === 'counts') body = { location: this.location, notes,
      lines: this.inputs(this.lines).map((x, i) => ({ ...x, expectedRevision: this.lines[i].expectedRevision })) };
    else body = { location: this.location, notes, lines: this.inputs(this.lines) };
    await this.save(() => this.api.record(this.action, body), 'Movimiento registrado. Las existencias ya están actualizadas.', () => {
      this.lines = [this.blankLine()]; this.notes = ''; this.productionQuantity = null; this.productionDiscarded = 0;
    });
  }

  beginReceive(transfer: Transfer): void {
    this.receiveId = transfer.id;
    this.receiveNotes = '';
    this.receiveLines = transfer.lines.map(x => ({ itemId: x.itemId, quantity: x.sent, unit: this.baseUnit(this.item(x.itemId)!.unit) }));
    this.setTab('register');
  }
  receivingTransfer(): Transfer | undefined { return this.transfers().find(x => x.id === this.receiveId); }
  receivedDifference(): boolean {
    return this.receiveLines.some(x => this.baseQuantity(x) !== this.receivingTransfer()?.lines.find(l => l.itemId === x.itemId)?.sent);
  }
  get canReceive(): boolean {
    return this.validLines(this.receiveLines, true) && (!this.receivedDifference() || !!this.receiveNotes.trim())
      && this.receiveLines.every(x => this.baseQuantity(x) <= (this.receivingTransfer()?.lines.find(l => l.itemId === x.itemId)?.sent ?? 0));
  }
  async receive(): Promise<void> {
    if (!this.canReceive || this.busy()) return;
    await this.save(() => this.api.record(`transfers/${this.receiveId}/receive`, { notes: this.receiveNotes.trim(), lines: this.inputs(this.receiveLines) }),
      'Recepción confirmada. Las diferencias quedaron registradas.', () => { this.receiveId = ''; });
  }

  newItem(): void { this.itemId = ''; this.itemName = ''; this.itemProductId = ''; this.itemKind = 'Ingredient'; this.itemUnit = 'Unit'; this.itemMinimum = 0; }
  editItem(item: StockItem): void {
    this.itemId = item.id; this.itemName = item.name; this.itemKind = item.kind; this.itemUnit = item.unit;
    this.itemProductId = item.productId ?? ''; this.itemMinimum = item.minimumQuantity;
    this.setTab('items');
  }
  changeKind(): void { this.itemProductId = ''; if (this.itemKind === 'FinishedProduct') this.itemUnit = 'Unit'; }
  selectProduct(): void { this.itemName = this.products().find(x => x.id === this.itemProductId)?.name ?? ''; }
  get canSaveItem(): boolean {
    return !!this.itemName.trim() && this.itemMinimum !== null && this.itemMinimum >= 0
      && (this.itemKind === 'Ingredient' || !!this.itemProductId);
  }
  async saveItem(): Promise<void> {
    if (!this.canSaveItem || this.busy()) return;
    await this.save(async () => {
      if (this.itemId) await this.api.updateItem(this.itemId, this.itemName.trim(), this.itemMinimum!);
      else await this.api.createItem({ name: this.itemName.trim(), kind: this.itemKind, unit: this.itemUnit,
        productId: this.itemProductId || null, minimumQuantity: this.itemMinimum! });
    }, 'Artículo guardado. Podés registrar su stock inicial desde Ingreso / compra.', () => this.newItem());
  }

  selectRecipeItem(): void {
    const recipe = this.data().recipes.find(x => x.itemId === this.recipeItemId);
    this.recipeYield = recipe?.yield ?? 10;
    this.recipeLines = recipe ? recipe.lines.map(x => ({ itemId: x.itemId, quantity: x.quantity, unit: this.baseUnit(this.item(x.itemId)!.unit) })) : [this.blankLine()];
  }
  editRecipe(recipe: Recipe): void { this.recipeItemId = recipe.itemId; this.selectRecipeItem(); }
  get canSaveRecipe(): boolean {
    return !!this.recipeItemId && this.recipeYield !== null && this.recipeYield > 0 && Number.isInteger(this.recipeYield)
      && this.validLines(this.recipeLines) && this.recipeLines.every(x => this.item(x.itemId)?.kind === 'Ingredient');
  }
  async saveRecipe(): Promise<void> {
    if (!this.canSaveRecipe || this.busy()) return;
    await this.save(async () => { await this.api.saveRecipe(this.recipeItemId, this.recipeYield!, this.inputs(this.recipeLines)); },
      'Receta guardada. Las producciones anteriores conservan su receta original.', () => {
        this.recipeItemId = ''; this.recipeLines = [this.blankLine()]; this.recipeYield = 10;
      });
  }

  async loadHistory(page: number): Promise<void> {
    const request = ++this.historyRequest;
    this.error.set(null);
    try {
      const history = await this.api.history(page, this.historyItemId, this.historyLocation);
      if (request === this.historyRequest) { this.history.set(history); this.historyPage = page; }
    } catch (error) { if (request === this.historyRequest) this.error.set(toUserMessage(error, 'No fue posible cargar el historial.')); }
  }
  operationLabel(type: string): string {
    return ({ Receipt: 'Ingreso', Production: 'Producción', TransferSent: 'Envío', TransferReceived: 'Recepción',
      Waste: 'Merma', Count: 'Conteo y ajuste', Sale: 'Venta entregada' } as Record<string, string>)[type] ?? type;
  }

  private async save(action: () => Promise<void>, message: string, reset: () => void): Promise<void> {
    this.busy.set(true); this.error.set(null); this.success.set(null);
    try {
      await action();
      reset();
      this.success.set(message);
      try { await this.fetchData(); }
      catch { this.error.set('Se guardó la operación, pero no se pudo actualizar la vista. Usá Actualizar para ver las existencias.'); }
    } catch (error) { this.error.set(toUserMessage(error, 'No fue posible guardar. Revisá los datos y reintentá.')); }
    finally { this.busy.set(false); }
  }
}
