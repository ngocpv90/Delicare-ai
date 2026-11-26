import { DecimalPipe } from '@angular/common';
import { Component, Input, inject, signal } from '@angular/core';
import { CartService } from './cart.service';
import { PortalProduct } from './portal.model';

@Component({
  selector: 'app-portal-product-card',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    <article class="pt-card pt-product">
      <div class="pt-product-img" [class.tone-orange]="product.tone === 'orange'">
        <i class="material-icons">{{ product.icon }}</i>
        @if (product.tags.length) {
          <div class="pt-tags">
            @for (t of product.tags; track t) {
              <span class="pt-tag">{{ t }}</span>
            }
          </div>
        }
      </div>
      <div class="pt-product-body">
        <h3>{{ product.name }}</h3>
        <p>{{ product.description }}</p>
        <div class="pt-product-foot">
          <div class="pt-price">
            {{ product.price | number: '1.0-0' : 'vi' }}đ<small>/{{ product.unit }}</small>
          </div>
          <button type="button" class="pt-btn pt-btn-sm" [class.done]="added()" (click)="add()">
            <i class="material-icons">{{ added() ? 'check' : 'add_shopping_cart' }}</i>
            {{ added() ? 'Đã thêm' : 'Thêm' }}
          </button>
        </div>
        @if (cart.quantityOf(product.id) > 0) {
          <div class="pt-incart">Trong giỏ: {{ cart.quantityOf(product.id) }} {{ product.unit }}</div>
        }
      </div>
    </article>
  `,
})
export class PortalProductCardComponent {
  @Input({ required: true }) public product!: PortalProduct;
  public readonly cart = inject(CartService);
  public readonly added = signal(false);
  private timer?: ReturnType<typeof setTimeout>;

  public add(): void {
    this.cart.add(this.product);
    this.added.set(true);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.added.set(false), 1200);
  }
}
