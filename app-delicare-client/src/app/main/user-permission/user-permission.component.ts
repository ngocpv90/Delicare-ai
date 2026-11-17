import { Component, OnInit, Renderer2 } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  checkClassActive,
  isGroupMenu,
  permission,
  toFormData,
} from 'src/app/helpers/common-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { UserGroupService } from 'src/app/services/user-group.service';
import { UserRoleService } from 'src/app/services/user-role.service';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { isApprovalMenu, PERMISSION_FLAGS } from './permission-flags';
import { UserPermissionCopyModalComponent } from './user-permission-copy-modal/user-permission-copy-modal.component';

// adm-permission – Ma trận quyền Nhóm × Chức năng (FE-1).
// Matrix = UserRole/GetMatrix (every active menu, rows without a UserRole have id 00000000-…).
// Changes are kept locally and sent together: UserRole/Update, form field ListPermission = JSON array.
// Same rules as the API (spec 3.3): ticking any flag ticks Access; unticking Access clears the row.
// A level-1 group is only shown in the sidebar when its own Access is on, so ticking a screen ticks its group.
@Component({
  selector: 'app-user-permission',
  templateUrl: './user-permission.component.html',
})
export class UserPermissionComponent implements OnInit {
  public permission: any;
  public isAdmin = false;
  public isLoading = false;
  public isLoadingGroup = false;
  public listGroup: any[] = [];
  public groupId: string | null = null;
  public matrix: any[] = [];
  public searchText = '';
  public readonly flags = PERMISSION_FLAGS;
  private snapshot = new Map<string, string>();

  // Preview drawer (admin only)
  public previewOpen = false;
  public preview: any;

  constructor(
    private authenticationService: AuthenticationService,
    private userGroupService: UserGroupService,
    private userRoleService: UserRoleService,
    private notify: NotifyService,
    private modalService: ModalService,
    private route: ActivatedRoute,
    private router: Router,
    private renderer: Renderer2
  ) {}

  public ngOnInit(): void {
    const listMenu = this.authenticationService.userValue?.listMenu;
    checkClassActive(this.router, this.renderer, listMenu);
    this.permission = permission(this.router.url, listMenu);
    this.isAdmin = !!this.authenticationService.userValue?.isAdmin;
    if (this.permission?.allowAccess) {
      this.getGroups(this.route.snapshot.queryParamMap.get('groupId'));
    }
  }

  public get group(): any {
    return this.listGroup.find((x) => x.id === this.groupId);
  }

  private getGroups(selectId?: string | null): void {
    this.isLoadingGroup = true;
    this.userGroupService.getAllData().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.listGroup = [...(data.listData ?? [])].sort((a, b) =>
            (a.name ?? '').localeCompare(b.name ?? '', 'vi')
          );
          const first = this.listGroup.find((x) => x.id === selectId) ?? this.listGroup[0];
          if (first) {
            this.groupId = first.id;
            this.getMatrix();
          }
        }
      },
      () => {
        this.isLoadingGroup = false;
        this.notify.error();
      },
      () => {
        this.isLoadingGroup = false;
      }
    );
  }

  public async selectGroup(id: string): Promise<void> {
    if (id === this.groupId) {
      return;
    }
    if (
      this.changedRows().length &&
      !(await this.notify.confirmDelete('Có thay đổi chưa lưu. Bỏ các thay đổi và chuyển nhóm?'))
    ) {
      return;
    }
    this.groupId = id;
    this.router.navigate([], { relativeTo: this.route, queryParams: { groupId: id }, replaceUrl: true });
    this.getMatrix();
  }

  private getMatrix(after?: () => void): void {
    if (!this.groupId) {
      return;
    }
    this.isLoading = true;
    this.userRoleService.getMatrix(this.groupId).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.matrix = data.data?.listMenu ?? [];
          this.takeSnapshot();
          after?.();
        } else {
          this.matrix = [];
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

  // ===== Rows =====
  private allRows(): any[] {
    return this.matrix.flatMap((x) => [x, ...(x.children ?? [])]);
  }

  private key(row: any): string {
    return this.flags.map((f) => (row[f.key] ? 1 : 0)).join('');
  }

  private takeSnapshot(): void {
    this.snapshot = new Map(this.allRows().map((x) => [x.objectMenuId, this.key(x)]));
  }

  public isDirty(row: any): boolean {
    return this.snapshot.get(row.objectMenuId) !== this.key(row);
  }

  public changedRows(): any[] {
    return this.allRows().filter((x) => this.isDirty(x));
  }

  // Level-1 group row ('#/group/NN' or a menu with children): only the Access flag matters
  public isGroupRow(row: any): boolean {
    return isGroupMenu(row) || !!row.children?.length;
  }

  public isApproval(row: any): boolean {
    return isApprovalMenu(row.metaLink);
  }

  public visibleChildren(root: any): any[] {
    const text = this.searchText.trim().toLowerCase();
    const children: any[] = root.children ?? [];
    if (!text || (root.objectMenuName ?? '').toLowerCase().includes(text)) {
      return children;
    }
    return children.filter(
      (x) =>
        (x.objectMenuName ?? '').toLowerCase().includes(text) ||
        (x.metaLink ?? '').toLowerCase().includes(text)
    );
  }

  public isRootVisible(root: any): boolean {
    const text = this.searchText.trim().toLowerCase();
    return (
      !text ||
      (root.objectMenuName ?? '').toLowerCase().includes(text) ||
      (root.metaLink ?? '').toLowerCase().includes(text) ||
      this.visibleChildren(root).length > 0
    );
  }

  public toggle(row: any, flag: string, checked: boolean, parent?: any): void {
    row[flag] = checked;
    if (flag === 'allowAccess' && !checked) {
      this.flags.forEach((f) => (row[f.key] = false));
    } else if (checked) {
      row.allowAccess = true;
    }
    if (row.allowAccess && parent && !parent.allowAccess) {
      parent.allowAccess = true;
    }
  }

  // Whole row on / off (all 7 flags)
  public toggleRow(row: any, parent?: any): void {
    const on = !this.flags.every((f) => row[f.key]);
    this.flags.forEach((f) => (row[f.key] = on));
    if (on && parent && !parent.allowAccess) {
      parent.allowAccess = true;
    }
  }

  public discard(): void {
    this.getMatrix();
  }

  // ===== Save =====
  public save(): void {
    this.sendRows(this.changedRows(), true);
  }

  private sendRows(rows: any[], notifyResult: boolean): void {
    if (!rows.length || !this.groupId) {
      return;
    }
    const list = rows.map((x) => ({
      id: x.id,
      userGroupId: this.groupId,
      objectMenuId: x.objectMenuId,
      allowAccess: !!x.allowAccess,
      allowAdd: !!x.allowAdd,
      allowEdit: !!x.allowEdit,
      allowDelete: !!x.allowDelete,
      allowPrint: !!x.allowPrint,
      allowExport: !!x.allowExport,
      allowImport: !!x.allowImport,
    }));
    this.isLoading = true;
    this.userRoleService.update(toFormData({ listPermission: JSON.stringify(list) })).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          if (notifyResult) {
            this.notify.success(data.responseMessage);
          }
          this.getMatrix();
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

  // SaveAccessAll only touches level-2 menus: after granting, also open the level-1 groups (sidebar)
  public async accessAll(allowAccess: boolean): Promise<void> {
    if (!this.groupId) {
      return;
    }
    const pending = this.changedRows().length ? ' Các thay đổi chưa lưu sẽ bị bỏ.' : '';
    const question = allowAccess
      ? `Cấp quyền truy cập mọi màn hình cho nhóm "${this.group?.name}"?${pending}`
      : `Thu hồi toàn bộ quyền (mọi màn hình) của nhóm "${this.group?.name}"?${pending}`;
    if (!(await this.notify.confirmDelete(question))) {
      return;
    }
    this.isLoading = true;
    this.userRoleService
      .saveAccessAll(toFormData({ userGroupId: this.groupId, allowAccess }))
      .subscribe(
        (data) => {
          if (data.responseCode === 200) {
            this.notify.success(data.responseMessage);
            this.getMatrix(() => {
              if (allowAccess) {
                const groups = this.matrix.filter(
                  (x) => x.children?.some((c: any) => c.allowAccess) && !x.allowAccess
                );
                groups.forEach((x) => (x.allowAccess = true));
                this.sendRows(groups, false);
              }
            });
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

  public openCopy(): void {
    this.modalService
      .openForResult(UserPermissionCopyModalComponent, {
        title: 'Sao chép quyền từ nhóm khác',
        data: { target: this.group, listGroup: this.listGroup },
        size: 'sm',
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.getMatrix();
        }
      });
  }

  // ===== Preview (admin only) =====
  public openPreview(): void {
    if (!this.groupId) {
      return;
    }
    this.isLoading = true;
    this.userRoleService.getPreview(this.groupId).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.preview = data.data;
          this.previewOpen = true;
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
}
