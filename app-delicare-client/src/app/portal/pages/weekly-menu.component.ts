import { AsyncPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PortalDataService } from '../portal-data.service';

@Component({
  selector: 'app-portal-weekly-menu',
  standalone: true,
  imports: [AsyncPipe, RouterLink],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-page-head">
        <div class="pt-wrap">
          <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Thực đơn tuần</nav>
          <h1>{{ d.weeklyMenu.title }}</h1>
          <p>{{ d.weeklyMenu.note }}</p>
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap">
          <div class="pt-chips" role="tablist" aria-label="Chọn buổi">
            <button type="button" class="pt-chip" [class.active]="meal() === ''" (click)="meal.set('')">Cả ngày</button>
            @for (m of d.weeklyMenu.meals; track m) {
              <button type="button" class="pt-chip" [class.active]="meal() === m" (click)="meal.set(m)">Bữa {{ m.toLowerCase() }}</button>
            }
          </div>

          <div class="pt-menu-grid">
            @for (day of d.weeklyMenu.days; track day.day; let i = $index) {
              <article class="pt-card pt-menu-day" [class.is-today]="i === todayIndex">
                <header>
                  <h3>{{ day.day }}</h3>
                  @if (i === todayIndex) {
                    <span class="pt-tag">Hôm nay</span>
                  }
                </header>
                @for (m of d.weeklyMenu.meals; track m) {
                  @if ((meal() === '' || meal() === m) && day.meals[m]; as dishes) {
                    <div class="pt-menu-meal">
                      <span class="pt-meal-chip">{{ m }}</span>
                      <ul>
                        @for (dish of dishes; track dish) {
                          <li>{{ dish }}</li>
                        }
                      </ul>
                    </div>
                  }
                }
              </article>
            }
          </div>

          <div class="pt-center pt-mt">
            <a class="pt-btn pt-btn-lg" routerLink="/san-pham"><i class="material-icons">restaurant</i>Chọn suất và đặt hàng</a>
          </div>
        </div>
      </section>
    }
  `,
})
export class PortalWeeklyMenuComponent {
  public readonly data$ = inject(PortalDataService).data$;
  public readonly meal = signal('');
  public readonly todayIndex = new Date().getDay() - 1; // Thứ Hai = 0; Chủ nhật = -1 (không tô)
}
