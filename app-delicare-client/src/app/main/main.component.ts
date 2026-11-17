import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { flattenMenu, menuLink } from '../helpers/common-helper';
import {
  BRANCH_KEY,
  DOMAIN_ROUTER,
  HOME_ROUTE,
  LEGAL_NAME,
  LOGO_HEADER_NAME,
  PROJECT_SHORT_NAME,
  SOURCE,
  STRING_EMPTY,
} from '../helpers/constant-helper';
import { AuthenticationService } from '../services/authentication.service';
import { MetaDataService } from '../services/meta-data.service';
import { OrganizationUnitService } from '../services/organization-unit.service';
import { ModalService } from '../shared/modal.service';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { ProfileComponent } from './profile/profile.component';

@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
})
export class MainComponent implements OnInit, OnDestroy {
  public avatar = STRING_EMPTY;
  public fullName = STRING_EMPTY;
  public userName = STRING_EMPTY;
  public userGroupName = STRING_EMPTY;
  public legalName = LEGAL_NAME;
  public projectShortName = PROJECT_SHORT_NAME;
  public logoName = LOGO_HEADER_NAME;
  public homeRouter = SOURCE.concat(DOMAIN_ROUTER, SOURCE, HOME_ROUTE);
  public currentYear = new Date().getFullYear();
  public toggleVal = false;
  public mouseOvered = false;
  public menuSearch = STRING_EMPTY;
  public menuLeaves: any[] = [];
  public isFullscreen = false;
  public branches: any[] = [];
  public branchId = STRING_EMPTY;
  private subs: Subscription[] = [];

  constructor(
    private authenticationService: AuthenticationService,
    private organizationUnitService: OrganizationUnitService,
    private metaDataService: MetaDataService,
    private modalService: ModalService,
    private router: Router
  ) {
    this.subs.push(
      this.router.events
        .pipe(filter((e) => e instanceof NavigationEnd))
        .subscribe(() => setTimeout(() => this.handleMinHeight(), 500))
    );
  }

  public ngOnInit(): void {
    this.subs.push(
      this.authenticationService.user.subscribe((user) => {
        this.avatar = user?.avatar ?? STRING_EMPTY;
        this.fullName = user?.fullName ?? STRING_EMPTY;
        this.userName = user?.userName ?? STRING_EMPTY;
        this.userGroupName = user?.userGroupName ?? STRING_EMPTY;
        this.menuLeaves = flattenMenu(user?.listMenu);
      })
    );
    // D-36: light theme only (the template's light / dark switch is removed)
    localStorage.removeItem('data-theme-version');
    document.body.setAttribute('data-theme-version', 'light');
    this.loadBranches();
    this.onResize();
  }

  public ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
  }

  // Header branch selector: units with UnitType = BRANCH. The choice is kept per browser.
  private loadBranches(): void {
    this.organizationUnitService.getBranches().subscribe({
      next: (res) => {
        if (res?.responseCode !== 200) {
          return;
        }
        this.branches = res.listData ?? [];
        const saved = localStorage.getItem(BRANCH_KEY);
        const branch = this.branches.find((x) => x.id === saved) ?? this.branches[0];
        this.branchId = branch?.id ?? STRING_EMPTY;
        if (this.branchId) {
          localStorage.setItem(BRANCH_KEY, this.branchId);
        }
      },
      error: () => (this.branches = []),
    });
  }

  public changeBranch(id: string): void {
    this.branchId = id;
    localStorage.setItem(BRANCH_KEY, id);
  }

  public searchMenu(): void {
    const text = this.menuSearch.trim().toLowerCase();
    if (!text) {
      return;
    }
    const menu =
      this.menuLeaves.find((x) => x.objectMenuName?.toLowerCase() === text) ??
      this.menuLeaves.find((x) => x.objectMenuName?.toLowerCase().includes(text));
    if (menu) {
      const link = menuLink(menu.metaLink);
      this.router.navigate([link.path], { queryParams: link.queryParams });
      this.menuSearch = STRING_EMPTY;
    }
  }

  public toggleFullscreen(): void {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen?.();
      this.isFullscreen = true;
    } else {
      document.exitFullscreen?.();
      this.isFullscreen = false;
    }
  }

  public logOut(): void {
    this.metaDataService.clear();
    this.authenticationService.logout();
  }

  public openModalInfo(): void {
    this.modalService.open(ProfileComponent, {
      title: this.modalTitle(),
      data: {},
    });
  }

  public openModalChangePassword(): void {
    this.modalService.open(ChangePasswordComponent, {
      title: 'Đổi mật khẩu',
      data: {},
    });
  }

  private modalTitle(): string {
    return 'Thông tin cá nhân '.concat(
      '- (Tên đăng nhập: ',
      this.authenticationService.userValue?.userName ?? STRING_EMPTY,
      ')'
    );
  }

  public sidebarToggle(): void {
    this.toggleVal = !this.toggleVal;
    setTimeout(() => this.handleMinHeight(), 500);
  }

  public iconHoverToggle(value: boolean): void {
    this.mouseOvered = value;
  }

  // Template breakpoints: mini sidebar on tablets, overlay on phones
  @HostListener('window:resize')
  public onResize(): void {
    const width = window.innerWidth;
    let sidebarStyle = localStorage.getItem('data-sidebar-style') ?? 'full';
    if (width < 1200 && width > 768) {
      this.toggleVal = true;
      sidebarStyle = 'mini';
    } else if (width <= 768) {
      this.toggleVal = false;
      sidebarStyle = 'overlay';
    } else {
      this.toggleVal = false;
    }
    document.body.setAttribute('data-sidebar-style', sidebarStyle);
    this.handleMinHeight();
  }

  private handleMinHeight(): void {
    const contentBody = document.getElementsByClassName(
      'content-body'
    )[0] as HTMLElement;
    if (contentBody) {
      contentBody.style.minHeight = window.innerHeight - 75 + 'px';
    }
  }
}
