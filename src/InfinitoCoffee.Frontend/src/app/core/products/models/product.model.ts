export interface Product {
  id: string;
  name: string;
  description: string | null;
  price: number;
  cost: number;
  canConsumeOnSite?: boolean;
  canTakeAway?: boolean;
  takeAwayCost?: number;
  categoryId: string | null;
  isActive: boolean;
  minimumQuantity?: number | null;
  minimumQuantityBranch2?: number | null;
  stockMode?: 'Stocked' | 'MadeToOrder';
}
