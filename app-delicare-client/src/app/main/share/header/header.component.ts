import { Component, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { findMenuByUrl } from 'src/app/helpers/common-helper';
import {
  DOMAIN_ROUTER,
  HOME_ROUTE,
  SOURCE,
  STRING_EMPTY,
} from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';

// Breadcrumb bar (.page-titles) of the layout for old screens. Screens using shared/page-header hide it
// (body.has-page-header) because the page header has its own breadcrumb.
@Component({
  selector: 'app-header',
  templateUrl: './header.component.html',
})
export class HeaderComponent implements OnInit {
  public titleHeader = STRING_EMPTY;
  public titlePage = STRING_EMPTY;
  public isShowBreadcrumb = false;
  public title = 'Trang chủ';
  public titleSubPage = STRING_EMPTY;
  public domainRouter = SOURCE.concat(DOMAIN_ROUTER, SOURCE, HOME_ROUTE);

  constructor(
    private router: Router,
    private authenticationService: AuthenticationService
  ) {}

  public ngOnInit(): void {
    this.update(this.router.url);
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe((e) => this.update((e as NavigationEnd).urlAfterRedirects));
  }

  private update(url: string): void {
    const listMenu = this.authenticationService.userValue?.listMenu ?? [];
    const menu = findMenuByUrl(url, listMenu);
    this.titleSubPage = menu?.objectMenuName ?? STRING_EMPTY;
    this.titleHeader = menu?.objectMenuName ?? STRING_EMPTY;
    this.titlePage =
      listMenu.find((x: any) => x.id === menu?.parentId)?.objectMenuName ?? STRING_EMPTY;
    this.isShowBreadcrumb = !!this.titlePage;
  }
}
