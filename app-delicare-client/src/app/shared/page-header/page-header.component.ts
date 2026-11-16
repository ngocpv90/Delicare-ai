import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { findMenuByUrl, menuUrl } from '../../helpers/common-helper';
import { DOMAIN_ROUTER, HOME_ROUTE, SOURCE } from '../../helpers/constant-helper';
import { AuthenticationService } from '../../services/authentication.service';

export interface Crumb {
  label: string;
  link?: string;
}

// D-36 page header: breadcrumb -> title -> short description -> action buttons on the right.
// <app-page-header description="…"> <button class="btn btn-primary">+ Tạo mới</button> </app-page-header>
// Title / breadcrumb default to the current ObjectMenu item (listMenu) so screens only pass what differs.
@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './page-header.component.html',
})
export class PageHeaderComponent implements OnInit, OnChanges, OnDestroy {
  @Input() public title?: string;
  @Input() public description?: string;
  @Input() public breadcrumb?: Crumb[];

  public homeLink = SOURCE.concat(DOMAIN_ROUTER, SOURCE, HOME_ROUTE);
  public crumbs: Crumb[] = [];
  public pageTitle = '';

  constructor(
    private router: Router,
    private authenticationService: AuthenticationService
  ) {}

  public ngOnInit(): void {
    // The layout's breadcrumb bar (main/share/header) is hidden while a page header is shown
    document.body.classList.add('has-page-header');
    this.build();
  }

  // Re-run when the inputs change (a reused route component, e.g. the placeholder screen)
  public ngOnChanges(): void {
    this.build();
  }

  private build(): void {
    const listMenu = this.authenticationService.userValue?.listMenu ?? [];
    const menu = findMenuByUrl(this.router.url, listMenu);
    const group = menu?.parentId ? listMenu.find((x: any) => x.id === menu.parentId) : null;
    this.pageTitle = this.title ?? menu?.objectMenuName ?? '';
    this.crumbs =
      this.breadcrumb ??
      [
        ...(group ? [{ label: group.objectMenuName }] : []),
        ...(this.pageTitle ? [{ label: this.pageTitle, link: menu ? menuUrl(menu.metaLink) : undefined }] : []),
      ];
  }

  public ngOnDestroy(): void {
    document.body.classList.remove('has-page-header');
  }
}
