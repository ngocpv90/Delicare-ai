import { Directive, OnInit, Renderer2, Type, inject } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { checkClassActive, permission } from '../../helpers/common-helper';
import {
  ADD_MODAL_TITLE,
  DATE_FORMAT,
  STRING_EMPTY,
  UPDATE_MODAL_TITLE,
} from '../../helpers/constant-helper';
import { AuthenticationService } from '../../services/authentication.service';
import { ModalService } from '../modal.service';
import { NotifyService } from '../notify.service';
import { TableState } from '../table/table-state';
import { isEmptyFilter, localQuery } from './local-query';

// Shared behaviour of the FE-2 master-data list screens (same flow as main/unit-measure / main/employee:
// permission() from listMenu, searchForm, TableState + table-pager, modal form, confirm delete,
// responseCode 200 / 400 -> responseMessage). Rows come from loadRows() (GetAllData + lookups) and are
// searched / filtered / sorted / paged in the browser (see local-query.ts for why).
@Directive()
export abstract class MasterListBase implements OnInit {
  public listData: any[] = [];
  public allData: any[] = [];
  public searchForm!: UntypedFormGroup;
  public isLoading = false;
  public table = new TableState();
  public permission: any;
  public readonly dateFormat = DATE_FORMAT;

  protected authenticationService = inject(AuthenticationService);
  protected formBuilder = inject(UntypedFormBuilder);
  protected notify = inject(NotifyService);
  protected modalService = inject(ModalService);
  protected router = inject(Router);
  protected renderer = inject(Renderer2);

  // Row fields matched by the search box
  protected abstract searchFields: string[];
  // Filter controls of searchForm besides searchText, with their reset value
  protected filterDefaults: Record<string, any> = {};
  // Filter controls handled by matches() instead of exact match
  protected customFilters: string[] = [];

  protected abstract loadRows(): Observable<any[]>;
  protected abstract deleteRow(id: any): Observable<any>;

  // Extra filter for the controls in customFilters
  protected matches(_row: any, _filter: any): boolean {
    return true;
  }

  // Called once when the user can access the screen, before the first load (dropdown lists…)
  protected init(): void {}

  public ngOnInit(): void {
    const listMenu = this.authenticationService.userValue?.listMenu;
    checkClassActive(this.router, this.renderer, listMenu);
    this.permission = permission(this.router.url, listMenu);
    this.searchForm = this.formBuilder.group({
      searchText: [STRING_EMPTY],
      ...Object.fromEntries(Object.entries(this.filterDefaults).map(([k, v]) => [k, [v]])),
    });
    if (this.permission?.allowAccess) {
      this.init();
      this.reload();
    }
  }

  public reload(): void {
    this.isLoading = true;
    this.loadRows().subscribe({
      next: (rows) => {
        this.allData = rows ?? [];
        this.applyQuery();
      },
      error: () => {
        this.isLoading = false;
        this.notify.error();
      },
      complete: () => {
        this.isLoading = false;
      },
    });
  }

  protected applyQuery(): void {
    const value = this.searchForm.value;
    const filters: Record<string, any> = {};
    Object.keys(this.filterDefaults)
      .filter((k) => !this.customFilters.includes(k))
      .forEach((k) => (filters[k] = value[k]));
    const rows = this.allData.filter((row) => this.matches(row, value));
    const result = localQuery(rows, {
      searchText: value.searchText,
      searchFields: this.searchFields,
      filters,
      sortField: this.table.sortField,
      sortOrder: this.table.sortOrder,
      page: this.table.page,
      pageSize: this.table.pageSize,
    });
    if (!result.listData.length && result.total && this.table.page > 1) {
      // the last row of the last page was removed
      this.table.page = Math.ceil(result.total / this.table.pageSize);
      this.applyQuery();
      return;
    }
    this.listData = result.listData;
    this.table.total = result.total;
  }

  public search(): void {
    this.table.page = 1;
    this.applyQuery();
  }

  // Quick filter chips / dropdowns outside the search form submit
  public setFilter(name: string, value: any): void {
    this.searchForm.patchValue({ [name]: value });
    this.search();
  }

  public resetSearch(): void {
    this.searchForm.reset({ searchText: STRING_EMPTY, ...this.filterDefaults });
    this.search();
  }

  // Number of loaded rows with field = code (quick filter chips)
  public countOf(field: string, code?: any): number {
    return isEmptyFilter(code)
      ? this.allData.length
      : this.allData.filter((x) => String(x?.[field] ?? '').toLowerCase() === String(code).toLowerCase()).length;
  }

  public sort(field: string): void {
    this.table.toggleSort(field);
    this.applyQuery();
  }

  public changePage(page: number): void {
    this.table.page = page;
    this.applyQuery();
  }

  public changePageSize(size: number): void {
    this.table.pageSize = size;
    this.table.page = 1;
    this.applyQuery();
  }

  // Opens the create / edit modal; reloads the list when it saved
  protected openForm<T>(component: Type<T>, data?: any, size: 'sm' | 'lg' | 'xl' = 'lg', isEdit = !!data?.id): void {
    this.modalService
      .openForResult(component, {
        title: isEdit ? UPDATE_MODAL_TITLE : ADD_MODAL_TITLE,
        data,
        size,
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.afterSave();
        }
      });
  }

  protected afterSave(): void {
    this.reload();
  }

  public async delete(id: any): Promise<void> {
    if (!(await this.notify.confirmDelete())) {
      return;
    }
    this.isLoading = true;
    this.deleteRow(id).subscribe({
      next: (result) => {
        this.isLoading = false;
        if (result?.responseCode === 200) {
          this.notify.success(result.responseMessage);
          this.afterSave();
        } else {
          this.notify.error(result?.responseMessage);
        }
      },
      error: () => {
        this.isLoading = false;
        this.notify.error();
      },
    });
  }
}
