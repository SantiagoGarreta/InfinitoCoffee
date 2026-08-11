import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { OrderEntryStore } from '../data-access/order-entry.store';
import { OrderEntryPageComponent } from './order-entry-page.component';

class FakeOrderEntryStore {
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly menuSaving = signal(false);
  readonly menuError = signal<string | null>(null);
  readonly menuSuccessMessage = signal<string | null>(null);
  readonly activeCategories = signal([{ id: 'c-1', name: 'Cafe', isActive: true }]);
  readonly products = signal([
    { id: 'p-1', name: 'Espresso', description: null, price: 8, categoryId: 'c-1', isActive: true },
  ]);
  readonly visibleProducts = signal([
    { id: 'p-1', name: 'Espresso', description: null, price: 8, categoryId: 'c-1', isActive: true },
  ]);
  readonly selectedCategoryId = signal<string | null>('c-1');
  readonly source = signal<'Counter' | 'WhatsApp' | 'Web'>('Counter');
  readonly notes = signal('');
  readonly items = signal([]);
  readonly visualTotal = signal(0);
  readonly canSubmit = signal(false);
  initializeCalls = 0;

  initialize(): Promise<void> {
    this.initializeCalls++;
    return Promise.resolve();
  }

  selectCategory(): void {}
  setSource(): void {}
  setNotes(): void {}
  addProduct(): void {}
  clearMenuFeedback(): void {}
  createMenuProduct(): Promise<void> {
    return Promise.resolve();
  }
  updateMenuProduct(): Promise<void> {
    return Promise.resolve();
  }
  deleteMenuProduct(): Promise<void> {
    return Promise.resolve();
  }
  increaseQuantity(): void {}
  decreaseQuantity(): void {}
  updateItemNotes(): void {}
  submit(): Promise<void> {
    return Promise.resolve();
  }
}

describe('OrderEntryPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderEntryPageComponent],
      providers: [{ provide: OrderEntryStore, useClass: FakeOrderEntryStore }],
    }).compileComponents();
  });

  it('does not render a realtime connection status badge', () => {
    const fixture = TestBed.createComponent(OrderEntryPageComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-connection-status')).toBeNull();
  });

  it('initializes the order entry store on mount', () => {
    const fixture = TestBed.createComponent(OrderEntryPageComponent);
    const store = TestBed.inject(OrderEntryStore) as unknown as FakeOrderEntryStore;

    fixture.detectChanges();

    expect(store.initializeCalls).toBe(1);
  });

  it('keeps the submit button enabled when the store is idle, even if data is incomplete', () => {
    const fixture = TestBed.createComponent(OrderEntryPageComponent);
    fixture.detectChanges();

    const submitButton = fixture.nativeElement.querySelector('.order-entry__submit') as HTMLButtonElement;

    expect(submitButton.disabled).toBe(false);
  });

  it('opens the menu editor when pressing the menu button', () => {
    const fixture = TestBed.createComponent(OrderEntryPageComponent);
    fixture.detectChanges();

    const menuButton = fixture.nativeElement.querySelector('.order-entry__menu-button') as HTMLButtonElement;
    menuButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-menu-editor')).not.toBeNull();
  });
});

describe('OrderEntryPageComponent interactions', () => {
  class InteractiveOrderEntryStore {
    readonly loading = signal(false);
    readonly loadError = signal<string | null>(null);
    readonly submitting = signal(false);
    readonly submitError = signal<string | null>(null);
    readonly successMessage = signal<string | null>(null);
    readonly menuSaving = signal(false);
    readonly menuError = signal<string | null>(null);
    readonly menuSuccessMessage = signal<string | null>(null);
    readonly categories = signal([{ id: 'c-1', name: 'Cafe', isActive: true }]);
    readonly products = signal([
      { id: 'p-1', name: 'Espresso', description: null, price: 8, categoryId: 'c-1', isActive: true },
    ]);
    readonly selectedCategoryId = signal<string | null>('c-1');
    readonly source = signal<'Counter' | 'WhatsApp' | 'Web'>('Counter');
    readonly notes = signal('');
    readonly items = signal<Array<{ productId: string; productName: string; unitPrice: number; quantity: number; notes: string }>>([]);
    readonly activeCategories = signal(this.categories());
    readonly visibleProducts = signal(this.products());
    readonly visualTotal = signal(0);
    readonly canSubmit = signal(false);

    initialize(): Promise<void> {
      return Promise.resolve();
    }

    selectCategory(categoryId: string): void {
      this.selectedCategoryId.set(categoryId);
    }

    setSource(source: 'Counter' | 'WhatsApp' | 'Web'): void {
      this.source.set(source);
    }

    setNotes(notes: string): void {
      this.notes.set(notes);
    }

    addProduct(product: { id: string; name: string; price: number }): void {
      this.items.set([
        ...this.items(),
        { productId: product.id, productName: product.name, unitPrice: product.price, quantity: 1, notes: '' },
      ]);
      this.visualTotal.set(this.items().reduce((sum, item) => sum + (item.unitPrice * item.quantity), 0));
      this.recompute();
    }

    clearMenuFeedback(): void {}
    createMenuProduct(): Promise<void> {
      return Promise.resolve();
    }
    updateMenuProduct(): Promise<void> {
      return Promise.resolve();
    }
    deleteMenuProduct(): Promise<void> {
      return Promise.resolve();
    }
    increaseQuantity(): void {}
    decreaseQuantity(): void {}
    updateItemNotes(): void {}
    submit(): Promise<void> {
      return Promise.resolve();
    }

    private recompute(): void {
      this.canSubmit.set(this.items().length > 0);
    }
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderEntryPageComponent],
      providers: [{ provide: OrderEntryStore, useClass: InteractiveOrderEntryStore }],
    }).compileComponents();
  });

  it('keeps submit enabled after adding a product', () => {
    const fixture = TestBed.createComponent(OrderEntryPageComponent);
    fixture.detectChanges();

    const productButton = fixture.debugElement.query(By.css('.product-grid__card'));
    productButton.nativeElement.click();
    fixture.detectChanges();

    const submitButton = fixture.nativeElement.querySelector('.order-entry__submit') as HTMLButtonElement;
    expect(submitButton.disabled).toBe(false);
  });
});
