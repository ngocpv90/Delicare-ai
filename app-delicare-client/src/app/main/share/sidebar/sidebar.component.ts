import { Component, EventEmitter, OnDestroy, OnInit, Output } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import {
  findMenuByUrl,
  isGroupMenu,
  menuLink,
} from 'src/app/helpers/common-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { THEME_ICONS } from 'src/app/shared/theme-icons';

// Level-1 ObjectMenu (group) -> theme sidebar icon. Matched on ShortName first (seed: kpi, sales, menu,
// scm, production, qc, logistics, hr, approve, admin), then on keywords of the name.
const GROUP_ICONS: Record<string, string> = {
  kpi: 'Dashboard',
  sales: 'Core HR',
  menu: 'Reports',
  scm: 'Finance',
  production: 'Projects',
  qc: 'Performance',
  logistics: 'Apps',
  hr: 'Employees',
  approve: 'Tasks',
  admin: 'CMS',
};
const NAME_ICONS: [RegExp, string][] = [
  [/tong quan|dashboard|kpi/, 'Dashboard'],
  [/kinh doanh|don hang|khach hang/, 'Core HR'],
  [/thuc don|mon an|cong thuc/, 'Reports'],
  [/mua hang|kho|nha cung cap|danh muc/, 'Finance'],
  [/san xuat|gia thanh/, 'Projects'],
  [/qc|an toan/, 'Performance'],
  [/giao|dong goi|van chuyen/, 'Apps'],
  [/nhan su|tai khoan|nguoi dung/, 'Employees'],
  [/phe duyet|duyet/, 'Tasks'],
  [/quan tri|he thong|cau hinh/, 'CMS'],
];

// Sidebar built from listMenu (login / api/User/GetCurrent), never hard-coded:
// level 1 = group (MetaLink '#/group/NN'), level 2 = screens. Groups without visible items are hidden.
@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.component.html',
})
export class SidebarComponent implements OnInit, OnDestroy {
  @Output() public mouseHoverEvent = new EventEmitter<boolean>();

  public listMenu: any[] = [];
  public activeMenu: any = null;
  public openMenu: any = null;
  private subs: Subscription[] = [];

  constructor(
    private authenticationService: AuthenticationService,
    private router: Router
  ) {}

  public ngOnInit(): void {
    this.subs.push(
      this.authenticationService.user.subscribe((user) => {
        // A level-1 item is shown when it has children or is itself a screen (not an empty group)
        // Links are computed once here (a new queryParams object per change detection would loop)
        const withLink = (x: any) => ({ ...x, link: menuLink(x.metaLink) });
        this.listMenu = (user?.listMenu ?? [])
          .filter((x: any) => x.children?.length || !isGroupMenu(x))
          .map((x: any) => ({ ...withLink(x), children: (x.children ?? []).map(withLink) }));
        this.syncActiveMenu(this.router.url);
      }),
      this.router.events
        .pipe(filter((e) => e instanceof NavigationEnd))
        .subscribe((e) => this.syncActiveMenu((e as NavigationEnd).urlAfterRedirects))
    );
  }

  public ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
  }

  public iconFor(menu: any): string {
    const byShortName = GROUP_ICONS[(menu?.shortName ?? '').toLowerCase()];
    if (byShortName) {
      return THEME_ICONS[byShortName];
    }
    const name = (menu?.objectMenuName ?? '')
      .toLowerCase()
      .normalize('NFD')
      // after NFD the accents are separate non-ASCII marks: drop them, keep đ as d
      .replace(/[^\x00-\x7F]/g, (c: string) => (c === 'đ' ? 'd' : ''));
    const match = NAME_ICONS.find(([re]) => re.test(name));
    return THEME_ICONS[match ? match[1] : 'Tasks'];
  }

  public isCurrent(menu: any): boolean {
    return !!this.activeMenu && this.activeMenu === menu;
  }

  public isParentActive(menu: any): boolean {
    return menu.children?.some((x: any) => this.isCurrent(x)) ?? false;
  }

  public toggleMenu(menu: any): void {
    this.openMenu = this.openMenu === menu ? null : menu;
  }

  public hoverAdd(value: boolean): void {
    this.mouseHoverEvent.emit(value);
  }

  private syncActiveMenu(url: string): void {
    this.activeMenu = findMenuByUrl(url, this.listMenu) ?? null;
    const parent = this.listMenu.find((x) => this.isParentActive(x));
    if (parent) {
      this.openMenu = parent;
    }
  }
}
