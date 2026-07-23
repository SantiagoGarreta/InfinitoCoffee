export interface Product {
  id: string;
  name: string;
  description: string | null;
  price: number;
  categoryId: string;
  isActive: boolean;
}
