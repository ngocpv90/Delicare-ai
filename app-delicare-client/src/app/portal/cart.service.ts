import { Injectable, computed, signal } from '@angular/core';
import { CartLine, PortalProduct } from './portal.model';

const STORAGE_KEY = 'dc_portal_cart';
const MAX_QTY = 999;

/** Giỏ hàng của khách lẻ – lưu trong trình duyệt (localStorage), không gửi máy chủ. */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly lines = signal<CartLine[]>(this.load());

  public readonly items = this.lines.asReadonly();
  public readonly count = computed(() => this.lines().reduce((s, l) => s + l.quantity, 0));
  public readonly total = computed(() => this.lines().reduce((s, l) => s + l.quantity * l.price, 0));

  public add(p: PortalProduct, quantity = 1): void {
    const list = [...this.lines()];
    const i = list.findIndex((l) => l.productId === p.id);
    if (i >= 0) {
      list[i] = { ...list[i], quantity: Math.min(MAX_QTY, list[i].quantity + quantity) };
    } else {
      list.push({ productId: p.id, name: p.name, price: p.price, unit: p.unit, quantity });
    }
    this.save(list);
  }

  public setQuantity(productId: string, quantity: number): void {
    const q = Math.floor(Number(quantity) || 0);
    if (q <= 0) {
      this.remove(productId);
      return;
    }
    this.save(this.lines().map((l) => (l.productId === productId ? { ...l, quantity: Math.min(MAX_QTY, q) } : l)));
  }

  public remove(productId: string): void {
    this.save(this.lines().filter((l) => l.productId !== productId));
  }

  public clear(): void {
    this.save([]);
  }

  public quantityOf(productId: string): number {
    return this.lines().find((l) => l.productId === productId)?.quantity ?? 0;
  }

  private save(list: CartLine[]): void {
    this.lines.set(list);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    } catch {
      /* trình duyệt chặn bộ nhớ – giỏ chỉ giữ trong phiên */
    }
  }

  private load(): CartLine[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      const list = raw ? (JSON.parse(raw) as CartLine[]) : [];
      return Array.isArray(list) ? list.filter((l) => l && l.productId && l.quantity > 0) : [];
    } catch {
      return [];
    }
  }
}
