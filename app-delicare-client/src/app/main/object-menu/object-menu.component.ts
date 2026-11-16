import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, OnInit, Renderer2 } from '@angular/core';
import { Router } from '@angular/router';
import { checkClassActive, permission } from 'src/app/helpers/common-helper';
import {
  ADD_MODAL_TITLE,
  UPDATE_MODAL_TITLE,
} from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { ObjectMenuService } from 'src/app/services/object-menu.service';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { ObjectMenuModalComponent } from './object-menu-modal/object-menu-modal.component';

type ActiveFilter = 'all' | 'active' | 'inactive';

// adm-objectmenu – Cấu hình menu chức năng (FE-1). Tree = ObjectMenu/GetTree?includeInactive=true (2 levels).
// Reorder inside one level by drag & drop (Angular CDK) or the up / down buttons; every move is saved at once
// with ObjectMenu/UpdateOrder ([{ id, parentId, orderShow }] for the siblings). Moving to another parent = edit form.
@Component({
  selector: 'app-object-menu',
  templateUrl: './object-menu.component.html',
})
export class ObjectMenuComponent implements OnInit {
  public tree: any[] = [];
  public isLoading = false;
  public permission: any;
  public searchText = '';
  public activeFilter: ActiveFilter = 'all';
  public collapsed = new Set<string>();

  constructor(
    private authenticationService: AuthenticationService,
    private objectMenuService: ObjectMenuService,
    private notify: NotifyService,
    private modalService: ModalService,
    private router: Router,
    private renderer: Renderer2
  ) {}

  public ngOnInit(): void {
    const listMenu = this.authenticationService.userValue?.listMenu;
    checkClassActive(this.router, this.renderer, listMenu);
    this.permission = permission(this.router.url, listMenu);
    if (this.permission?.allowAccess) {
      this.getData();
    }
  }

  private getData(): void {
    this.isLoading = true;
    this.objectMenuService.getTree(true).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.tree = data.data ?? [];
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

  // ===== Filter (drag & drop only on the unfiltered tree, so the saved order is the real one) =====
  public get canReorder(): boolean {
    return !!this.permission?.allowEdit && !this.searchText.trim() && this.activeFilter === 'all';
  }

  private match(m: any): boolean {
    const text = this.searchText.trim().toLowerCase();
    const textOk =
      !text ||
      (m.name ?? '').toLowerCase().includes(text) ||
      (m.metaLink ?? '').toLowerCase().includes(text) ||
      (m.shortName ?? '').toLowerCase().includes(text);
    const activeOk =
      this.activeFilter === 'all' || (this.activeFilter === 'active' ? m.isActive : !m.isActive);
    return textOk && activeOk;
  }

  public visibleChildren(root: any): any[] {
    const children: any[] = root.children ?? [];
    return this.canReorder ? children : children.filter((x) => this.match(x));
  }

  public isRootVisible(root: any): boolean {
    return this.canReorder || this.match(root) || this.visibleChildren(root).length > 0;
  }

  public setFilter(filter: ActiveFilter): void {
    this.activeFilter = filter;
  }

  public get countActive(): number {
    return this.flat().filter((x) => x.isActive).length;
  }

  public get countAll(): number {
    return this.flat().length;
  }

  private flat(): any[] {
    return this.tree.flatMap((x) => [x, ...(x.children ?? [])]);
  }

  public toggleCollapse(id: string): void {
    if (this.collapsed.has(id)) {
      this.collapsed.delete(id);
    } else {
      this.collapsed.add(id);
    }
  }

  public collapseAll(collapse: boolean): void {
    this.collapsed = new Set(collapse ? this.tree.map((x) => x.id) : []);
  }

  // ===== Reorder =====
  public drop(list: any[], parentId: string | null, event: CdkDragDrop<any[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.saveOrder(list, parentId);
  }

  public move(list: any[], parentId: string | null, index: number, step: number): void {
    const target = index + step;
    if (target < 0 || target >= list.length) {
      return;
    }
    moveItemInArray(list, index, target);
    this.saveOrder(list, parentId);
  }

  // Keep the existing OrderShow numbers (e.g. 1001, 1002…) and hand them out in the new order;
  // duplicated / empty numbers fall back to a running sequence.
  private saveOrder(list: any[], parentId: string | null): void {
    const olds = list
      .map((x) => x.orderShow)
      .filter((x) => x !== null && x !== undefined)
      .sort((a, b) => a - b);
    const distinct = olds.length === list.length && new Set(olds).size === olds.length;
    const base = olds.length ? olds[0] : 1;
    const body = list.map((x, i) => ({
      id: x.id,
      parentId,
      orderShow: distinct ? olds[i] : base + i,
    }));
    this.isLoading = true;
    this.objectMenuService.updateOrder(body).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          body.forEach((b, i) => (list[i].orderShow = b.orderShow));
          this.notify.success(data.responseMessage);
        } else {
          this.notify.error(data.responseMessage);
          this.getData();
        }
      },
      () => {
        this.isLoading = false;
        this.notify.error();
        this.getData();
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  // ===== Actions =====
  public openModal(data?: any, parentId?: string): void {
    this.modalService
      .openForResult(ObjectMenuModalComponent, {
        title: data ? UPDATE_MODAL_TITLE : ADD_MODAL_TITLE,
        data: data ? { id: data.id } : { parentId: parentId ?? null },
        size: 'lg',
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.getData();
        }
      });
  }

  public toggleActive(row: any): void {
    this.isLoading = true;
    this.objectMenuService.saveIsActive(row.id).subscribe(
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

  public async delete(row: any): Promise<void> {
    const question = row.children?.length
      ? `Xóa nhóm menu "${row.name}"? Nhóm còn ${row.children.length} chức năng con.`
      : `Xóa chức năng "${row.name}"?`;
    if (!(await this.notify.confirmDelete(question))) {
      return;
    }
    this.isLoading = true;
    this.objectMenuService.delete(row.id).subscribe(
      (result) => {
        if (result.responseCode === 200) {
          this.notify.success(result.responseMessage);
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
}
