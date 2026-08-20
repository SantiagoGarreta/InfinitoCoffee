export interface Product {
  id: string;
  name: string;
  description: string | null;
  price: number;
  cost: number;
  categoryId: string;
  isActive: boolean;
}
