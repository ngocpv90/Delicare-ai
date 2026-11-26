import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { map } from 'rxjs';
import { PortalDataService } from '../portal-data.service';
import { PortalData } from '../portal.model';

@Component({
  selector: 'app-portal-policies',
  standalone: true,
  imports: [AsyncPipe, RouterLink, RouterLinkActive],
  template: `
    @if (data$ | async; as d) {
      @if (current(d); as p) {
        <section class="pt-page-head">
          <div class="pt-wrap">
            <nav class="pt-crumb"><a routerLink="/">Trang chủ</a> / <a routerLink="/chinh-sach">Chính sách</a> / {{ p.title }}</nav>
            <h1>{{ p.title }}</h1>
          </div>
        </section>

        <section class="pt-section">
          <div class="pt-wrap pt-policy-layout">
            <aside class="pt-card pt-policy-nav">
              @for (x of d.policies; track x.slug) {
                <a [routerLink]="['/chinh-sach', x.slug]" [class.active]="x.slug === p.slug">
                  <i class="material-icons">{{ x.icon }}</i>{{ x.title }}
                </a>
              }
            </aside>
            <article class="pt-card pt-policy-body">
              @for (s of p.sections; track s.heading) {
                <h2>{{ s.heading }}</h2>
                <ul>
                  @for (it of s.items; track it) {
                    <li>{{ it }}</li>
                  }
                </ul>
              }
              <div class="pt-policy-contact">
                <i class="material-icons">support_agent</i>
                <span>Cần hỗ trợ thêm? Gọi <a [href]="'tel:' + d.company.phoneHref">{{ d.company.phone }}</a> hoặc <a routerLink="/lien-he">gửi yêu cầu</a>.</span>
              </div>
            </article>
          </div>
        </section>
      }
    }
  `,
})
export class PortalPoliciesComponent {
  public readonly data$ = inject(PortalDataService).data$;
  private readonly slug = toSignal(inject(ActivatedRoute).paramMap.pipe(map((m) => m.get('slug') ?? '')), { initialValue: '' });

  public current(d: PortalData) {
    return d.policies.find((p) => p.slug === this.slug()) ?? d.policies[0];
  }
}
