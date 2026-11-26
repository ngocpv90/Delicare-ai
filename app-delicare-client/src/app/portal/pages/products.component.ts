import { AsyncPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PortalDataService } from '../portal-data.service';
import { PortalProductCardComponent } from '../product-card.component';
import { PortalData } from '../portal.model';

@Component({
  selector: 'app-portal-products',
  standalone: true,
  imports: [AsyncPipe, RouterLink, PortalProductCardComponent],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-page-head">
        <div class="pt-wrap">
          <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Sản phẩm</nav>
          <h1>Sản phẩm</h1>
          <p>Chọn suất ăn, combo và thêm vào giỏ. Giá đã gồm hộp đựng, chưa gồm phí giao với đơn nhỏ lẻ.</p>
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap">
          <div class="pt-toolbar">
            <div class="pt-chips">
              <button type="button" class="pt-chip" [class.active]="category() === ''" (click)="category.set('')">Tất cả</button>
              @for (c of d.categories; track c.id) {
                <button type="button" class="pt-chip" [class.active]="category() === c.id" (click)="category.set(c.id)">{{ c.name }}</button>
              }
            </div>
            <label class="pt-search">
              <i class="material-icons">search</i>
              <input type="search" placeholder="Tìm món…" [value]="keyword()" (input)="keyword.set($any($event.target).value)" aria-label="Tìm món" />
            </label>
          </div>

          @if (filtered(d); as list) {
            @if (list.length) {
              <div class="pt-grid-products">
                @for (p of list; track p.id) {
                  <app-portal-product-card [product]="p" />
                }
              </div>
            } @else {
              <div class="pt-empty">
                <i class="material-icons">search_off</i>
                <p>Không tìm thấy món phù hợp.</p>
                <button type="button" class="pt-btn pt-btn-ghost" (click)="reset()">Xem tất cả</button>
              </div>
            }
          }
        </div>
      </section>
    }
  `,
})
export class PortalProductsComponent {
  public readonly data$ = inject(PortalDataService).data$;
  public readonly category = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('nhom') ?? '');
  public readonly keyword = signal('');

  public filtered(d: PortalData) {
    const k = this.normalize(this.keyword());
    return d.products.filter(
      (p) =>
        (!this.category() || p.categoryId === this.category()) &&
        (!k || this.normalize(p.name + ' ' + p.description).includes(k)),
    );
  }

  public reset(): void {
    this.category.set('');
    this.keyword.set('');
  }

  /** So khớp không phân biệt hoa thường / dấu tiếng Việt. */
  private normalize(s: string): string {
    return s.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').trim();
  }
}
