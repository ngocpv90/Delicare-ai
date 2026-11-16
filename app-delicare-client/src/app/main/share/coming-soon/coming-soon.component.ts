import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { merge } from 'rxjs';
import { findMenuByUrl, flattenMenu, menuLink } from 'src/app/helpers/common-helper';
import { LEGAL_NAME, PROJECT_SHORT_NAME } from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';

// Home page ('trang-chu') and the placeholder for menu items whose screen is not built yet.
@Component({
  selector: 'app-coming-soon',
  templateUrl: './coming-soon.component.html',
})
export class ComingSoonComponent implements OnInit {
  public isHome = false;
  public menuName = '';
  public shortName = PROJECT_SHORT_NAME;
  public legalName = LEGAL_NAME;
  public fullName = '';
  public groups: { name: string; items: { name: string; link: any }[] }[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authenticationService: AuthenticationService
  ) {}

  public ngOnInit(): void {
    const user = this.authenticationService.userValue;
    const listMenu = user?.listMenu ?? [];
    this.fullName = user?.fullName ?? '';
    this.isHome = !!this.route.snapshot.data['home'];
    if (this.isHome) {
      // Quick links: the screens of the user's menu, grouped like the sidebar
      this.groups = listMenu
        .map((g: any) => ({
          name: g.objectMenuName,
          items: flattenMenu([g]).map((x: any) => ({ name: x.objectMenuName, link: menuLink(x.metaLink) })),
        }))
        .filter((g: any) => g.items.length);
      return;
    }
    // The component is reused between placeholder URLs: follow the URL
    merge(this.route.url, this.route.queryParams).subscribe(() => {
      this.menuName = findMenuByUrl(this.router.url, listMenu)?.objectMenuName ?? '';
    });
  }
}
