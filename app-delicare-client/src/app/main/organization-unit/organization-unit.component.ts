import { Component, OnInit, Renderer2 } from '@angular/core';
import { Router } from '@angular/router';
import { checkClassActive, permission } from 'src/app/helpers/common-helper';
import {
  ADD_MODAL_TITLE,
  UPDATE_MODAL_TITLE,
} from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { OrganizationUnitService } from 'src/app/services/organization-unit.service';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { labelOf, RECORD_STATUSES, UNIT_TYPES } from './org-options';
import { OrganizationUnitModalComponent } from './organization-unit-modal/organization-unit-modal.component';

interface TreeRow {
  node: any;
  depth: number;
  hasChildren: boolean;
}

// adm-orgunit – Sơ đồ tổ chức (FE-1). Tree = OrganizationUnit/GetTree (any depth, with manager name,
// employee / warehouse counts). Rendered as an indented table; search keeps the matching units and their parents.
@Component({
  selector: 'app-organization-unit',
  templateUrl: './organization-unit.component.html',
})
export class OrganizationUnitComponent implements OnInit {
  public tree: any[] = [];
  public rows: TreeRow[] = [];
  public isLoading = false;
  public permission: any;
  public searchText = '';
  public unitType: string | null = null;
  public collapsed = new Set<string>();
  public readonly unitTypes = UNIT_TYPES;
  public readonly statuses = RECORD_STATUSES;
  public readonly labelOf = labelOf;
  public total = 0;

  constructor(
    private authenticationService: AuthenticationService,
    private organizationUnitService: OrganizationUnitService,
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
    this.organizationUnitService.getTree().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.tree = data.data ?? [];
          this.total = this.count(this.tree);
          this.build();
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

  private count(nodes: any[]): number {
    return nodes.reduce((n, x) => n + 1 + this.count(x.children ?? []), 0);
  }

  private get filtering(): boolean {
    return !!this.searchText.trim() || !!this.unitType;
  }

  private match(node: any): boolean {
    const text = this.searchText.trim().toLowerCase();
    const textOk =
      !text ||
      (node.name ?? '').toLowerCase().includes(text) ||
      (node.code ?? '').toLowerCase().includes(text) ||
      (node.managerName ?? '').toLowerCase().includes(text);
    return textOk && (!this.unitType || node.unitType === this.unitType);
  }

  // Flatten the visible tree: collapsed branches hidden; when filtering, matches + their parents (all open)
  public build(): void {
    const walk = (nodes: any[], depth: number): TreeRow[] =>
      nodes.flatMap((node) => {
        const children: any[] = node.children ?? [];
        const childRows = walk(children, depth + 1);
        const self: TreeRow = { node, depth, hasChildren: children.length > 0 };
        if (this.filtering) {
          return this.match(node) || childRows.length ? [self, ...childRows] : [];
        }
        return this.collapsed.has(node.id) ? [self] : [self, ...childRows];
      });
    this.rows = walk(this.tree, 0);
  }

  public toggle(id: string): void {
    if (this.collapsed.has(id)) {
      this.collapsed.delete(id);
    } else {
      this.collapsed.add(id);
    }
    this.build();
  }

  public collapseAll(collapse: boolean): void {
    const ids: string[] = [];
    const walk = (nodes: any[]) =>
      nodes.forEach((x) => {
        if (x.children?.length) {
          ids.push(x.id);
          walk(x.children);
        }
      });
    if (collapse) {
      walk(this.tree);
    }
    this.collapsed = new Set(ids);
    this.build();
  }

  public resetSearch(): void {
    this.searchText = '';
    this.unitType = null;
    this.build();
  }

  public openModal(data?: any, parentId?: string): void {
    this.modalService
      .openForResult(OrganizationUnitModalComponent, {
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

  public async delete(node: any): Promise<void> {
    const question = node.children?.length
      ? `Xóa đơn vị "${node.name}"? Đơn vị còn ${node.children.length} đơn vị con.`
      : `Xóa đơn vị "${node.name}"?`;
    if (!(await this.notify.confirmDelete(question))) {
      return;
    }
    this.isLoading = true;
    this.organizationUnitService.delete(node.id).subscribe(
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
