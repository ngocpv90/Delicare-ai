import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PortalDataService } from '../portal-data.service';

@Component({
  selector: 'app-portal-about',
  standalone: true,
  imports: [AsyncPipe, RouterLink],
  template: `
    @if (data$ | async; as d) {
      <section class="pt-page-head">
        <div class="pt-wrap">
          <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / Giới thiệu</nav>
          <h1>{{ d.about.title }}</h1>
          <p>{{ d.company.slogan }}</p>
        </div>
      </section>

      <section class="pt-section">
        <div class="pt-wrap pt-about">
          <div class="pt-about-text">
            @for (para of d.about.paragraphs; track $index) {
              <p>{{ para }}</p>
            }
          </div>
          <div class="pt-about-values">
            @for (v of d.about.values; track v.title) {
              <div class="pt-feature pt-feature-row">
                <span class="pt-feature-icon"><i class="material-icons">{{ v.icon }}</i></span>
                <div>
                  <h3>{{ v.title }}</h3>
                  <p>{{ v.text }}</p>
                </div>
              </div>
            }
          </div>
        </div>
      </section>

      <section class="pt-section pt-section-alt">
        <div class="pt-wrap pt-grid-4">
          @for (h of d.highlights; track h.title) {
            <div class="pt-feature">
              <span class="pt-feature-icon"><i class="material-icons">{{ h.icon }}</i></span>
              <h3>{{ h.title }}</h3>
              <p>{{ h.text }}</p>
            </div>
          }
        </div>
      </section>
    }
  `,
})
export class PortalAboutComponent {
  public readonly data$ = inject(PortalDataService).data$;
}
