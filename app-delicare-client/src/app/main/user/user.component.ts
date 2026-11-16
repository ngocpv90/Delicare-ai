import { Component, OnInit, Renderer2 } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { checkClassActive, permission } from 'src/app/helpers/common-helper';
import {
  ADD_MODAL_TITLE,
  STRING_EMPTY,
  UPDATE_MODAL_TITLE,
} from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { UserGroupService } from 'src/app/services/user-group.service';
import { UserRoleService } from 'src/app/services/user-role.service';
import { UserService } from 'src/app/services/user.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { TableState } from 'src/app/shared/table/table-state';
import { PERMISSION_FLAGS } from '../user-permission/permission-flags';
import { UserModalComponent } from './user-modal/user-modal.component';

// adm-user – Tài khoản người dùng (FE-1). Lock / unlock = User/SaveIsActive, reset password = User/ResetPassword
// (admin only), effective permissions = UserRole/GetByUserId in the detail drawer.
@Component({
  selector: 'app-user',
  templateUrl: './user.component.html',
})
export class UserComponent implements OnInit {
  public listData: any[] = [];
  public searchForm!: UntypedFormGroup;
  public isLoading = false;
  public table = new TableState();
  public permission: any;
  public isAdmin = false;
  public isLoadingUserGroupId = false;
  public listUserGroup: any[] = [];

  // Detail drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'info';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'info', label: 'Thông tin' },
    { key: 'permission', label: 'Quyền hiệu lực' },
  ];
  public effectiveMenu: any[] = [];
  public isLoadingEffective = false;
  public readonly flags = PERMISSION_FLAGS;

  constructor(
    private authenticationService: AuthenticationService,
    private userService: UserService,
    private userGroupService: UserGroupService,
    private userRoleService: UserRoleService,
    private formBuilder: UntypedFormBuilder,
    private notify: NotifyService,
    private modalService: ModalService,
    private router: Router,
    private renderer: Renderer2
  ) {}

  public ngOnInit(): void {
    checkClassActive(
      this.router,
      this.renderer,
      this.authenticationService.userValue?.listMenu
    );
    this.permission = permission(
      this.router.url,
      this.authenticationService.userValue?.listMenu
    );
    this.isAdmin = !!this.authenticationService.userValue?.isAdmin;
    this.searchForm = this.formBuilder.group({
      userName: [STRING_EMPTY],
      fullName: [STRING_EMPTY],
      userGroupId: [null],
    });
    if (this.permission?.allowAccess) {
      this.getAllDataUserGroup();
      this.getData();
    }
  }

  private getAllDataUserGroup(): void {
    this.isLoadingUserGroupId = true;
    this.userGroupService.getAllData().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.listUserGroup = data.listData;
        }
      },
      () => {
        this.isLoadingUserGroupId = false;
        this.notify.error();
      },
      () => {
        this.isLoadingUserGroupId = false;
      }
    );
  }

  private getData(): void {
    this.isLoading = true;
    const { fullName, userName, userGroupId } = this.searchForm.value;
    const param = {
      ...this.table.toParams(),
      fullName: fullName ?? STRING_EMPTY,
      userName: userName ?? STRING_EMPTY,
      userGroupId: userGroupId ?? STRING_EMPTY,
    };
    this.userService.getAll(param).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.table.total = data.totalRecords;
          this.listData = data.listData;
        }
      },
      () => {
        this.isLoading = false;
        this.notify.error();
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  public search(): void {
    this.table.page = 1;
    this.getData();
  }

  public resetSearch(): void {
    this.searchForm.reset({ userName: STRING_EMPTY, fullName: STRING_EMPTY, userGroupId: null });
    this.search();
  }

  public sort(field: string): void {
    this.table.toggleSort(field);
    this.getData();
  }

  public changePage(page: number): void {
    this.table.page = page;
    this.getData();
  }

  public changePageSize(size: number): void {
    this.table.pageSize = size;
    this.table.page = 1;
    this.getData();
  }

  public openModal(data?: any): void {
    this.modalService
      .openForResult(UserModalComponent, {
        title: data ? UPDATE_MODAL_TITLE : ADD_MODAL_TITLE,
        data,
        size: 'lg',
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.drawerOpen = false;
          this.getData();
        }
      });
  }

  public async delete(id: any): Promise<void> {
    if (!(await this.notify.confirmDelete())) {
      return;
    }
    this.isLoading = true;
    this.userService.delete(id).subscribe(
      (result) => {
        if (result.responseCode === 200) {
          this.notify.success(result.responseMessage);
          if (this.listData.length === 1 && this.table.page > 1) {
            this.table.page--;
          }
          this.drawerOpen = false;
          this.getData();
        } else {
          this.notify.error(result.responseMessage);
        }
      },
      () => {
        this.isLoading = false;
        this.notify.error();
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  // Admin only (the API refuses others): password back to the initial one (= user name, GAP-ADM-02)
  public async resetPassword(row: any): Promise<void> {
    if (
      !(await this.notify.confirmDelete(
        `Đặt lại mật khẩu của tài khoản "${row.userName}" về mật khẩu khởi tạo?`
      ))
    ) {
      return;
    }
    this.isLoading = true;
    this.userService.resetPassword(row.id).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.notify.success(data.responseMessage);
        } else {
          this.notify.error(data.responseMessage);
        }
      },
      () => {
        this.isLoading = false;
        this.notify.error();
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  // Lock / unlock (toggle). The API refuses locking your own account (responseCode 400).
  public async toggleActive(row: any): Promise<void> {
    const question = row.isActive
      ? `Khóa tài khoản "${row.userName}"? Người dùng sẽ không đăng nhập được.`
      : `Mở khóa tài khoản "${row.userName}"?`;
    if (!(await this.notify.confirmDelete(question))) {
      return;
    }
    this.isLoading = true;
    this.userService.saveIsActive(row.id).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          row.isActive = !row.isActive;
          this.notify.success(data.responseMessage);
        } else {
          this.notify.error(data.responseMessage);
        }
      },
      () => {
        this.isLoading = false;
        this.notify.error();
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  // ===== Detail drawer =====
  public openDrawer(row: any, tab = 'info'): void {
    this.drawerRow = row;
    this.drawerTab = tab;
    this.drawerOpen = true;
    this.effectiveMenu = [];
    this.loadEffective();
  }

  private loadEffective(): void {
    this.isLoadingEffective = true;
    this.userRoleService.getByUserId(this.drawerRow.id).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.effectiveMenu = data.data?.listMenu ?? [];
        } else {
          this.notify.error(data.responseMessage);
        }
      },
      () => {
        this.isLoadingEffective = false;
        this.notify.error();
      },
      () => {
        this.isLoadingEffective = false;
      }
    );
  }

  // Screens (level-2 menus) the user can open
  public get effectiveScreenCount(): number {
    return this.effectiveMenu.reduce((n, x) => n + (x.children?.length ?? 0), 0);
  }
}
