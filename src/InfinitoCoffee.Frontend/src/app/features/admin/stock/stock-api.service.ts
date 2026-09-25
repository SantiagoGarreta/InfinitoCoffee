import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { APP_RUNTIME_CONFIG } from '../../../core/config/app-runtime-config';

export type Location = 'Factory' | 'Cafe' | 'Transit';
export type PhysicalLocation = 'Factory' | 'Cafe';
export type Unit = 'Unit' | 'Gram' | 'Milliliter';
export interface StockItem {
  id: string; name: string; kind: 'Ingredient' | 'FinishedProduct'; unit: Unit;
  productId?: string | null; minimumQuantity: number;
}
export interface Balance { itemId: string; location: Location; quantity: number; revision: string }
export interface Recipe {
  id: string; itemId: string; version: number; yield: number; createdAtUtc: string;
  lines: { itemId: string; quantity: number }[];
}
export interface StockDashboard { items: StockItem[]; balances: Balance[]; recipes: Recipe[] }
export interface Transfer {
  id: string; from: PhysicalLocation; to: PhysicalLocation; sentAtUtc: string; receivedAtUtc?: string | null;
  lines: { itemId: string; sent: number; received?: number | null }[];
}
export interface Movement { itemId: string; itemName: string; unit: Unit; location: Location; before: number; delta: number; after: number }
export interface Operation {
  id: string; type: string; createdAtUtc: string; actorId?: string; actorName: string; notes: string;
  recipeId?: string; recipeVersion?: number; orderId?: string; movements: Movement[];
}
export interface History { items: Operation[]; hasMore: boolean }
export interface LineInput { itemId: string; quantity: number; unit: string }

@Injectable({ providedIn: 'root' })
export class StockApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(APP_RUNTIME_CONFIG).apiBaseUrl}/api/stock`;
  private readonly pending = new Map<string, string>();

  dashboard(): Promise<StockDashboard> { return firstValueFrom(this.http.get<StockDashboard>(this.url)); }
  transfers(): Promise<Transfer[]> { return firstValueFrom(this.http.get<Transfer[]>(`${this.url}/transfers`)); }
  history(page: number, itemId = '', location = ''): Promise<History> {
    let params = new HttpParams().set('page', page);
    if (itemId) params = params.set('itemId', itemId);
    if (location) params = params.set('location', location);
    return firstValueFrom(this.http.get<History>(`${this.url}/history`, { params }));
  }
  createItem(item: Omit<StockItem, 'id'>): Promise<StockItem> {
    return firstValueFrom(this.http.post<StockItem>(`${this.url}/items`, item));
  }
  updateItem(id: string, name: string, minimumQuantity: number): Promise<StockItem> {
    return firstValueFrom(this.http.put<StockItem>(`${this.url}/items/${id}`, { name, minimumQuantity }));
  }
  saveRecipe(itemId: string, yieldQuantity: number, lines: LineInput[]): Promise<Recipe> {
    return firstValueFrom(this.http.post<Recipe>(`${this.url}/recipes`, { itemId, yield: yieldQuantity, lines }));
  }
  async record(path: string, body: object): Promise<void> {
    const key = JSON.stringify([path, body]);
    const operationId = this.pending.get(key) ?? crypto.randomUUID();
    this.pending.set(key, operationId);
    // Preserve the id on failure: retrying after a lost response must not double the stock movement.
    await firstValueFrom(this.http.post<void>(`${this.url}/${path}`, { ...body, operationId }));
    this.pending.delete(key);
  }
}
