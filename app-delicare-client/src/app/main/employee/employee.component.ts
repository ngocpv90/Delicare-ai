import { Component, OnInit, Renderer2 } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { checkClassActive, permission } from 'src/app/helpers/common-helper';
import {
  ADD_MODAL_TITLE,
  DATE_FORMAT,
  STRING_EMPTY,
  UPDATE_MODAL_TITLE,
} from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { EmployeeService } from 'src/app/services/employee.service';
import { OrganizationUnitService } from 'src/app/services/organization-unit.service';
import { PositionService } from 'src/app/services/position.service';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { TableState } from 'src/app/shared/table/table-state';
import { EMPLOYEE_STATUSES, labelOf } from '../organization-unit/org-options';
import { EmployeeModalComponent } from './employee-modal/employee-modal.component';

// adm-employee – Hồ sơ nhân viên (FE-1). Filters: search, unit, position, status (Employee/GetAll query).
@Component({
  selector: 'app-employee',
  templateUrl: './employee.component.html',
})
export class EmployeeComponent implements OnInit {
  public listData: any[] = [];
  public searchForm!: UntypedFormGroup;
  public isLoading = false;
  public table = new TableState();
  public permission: any;
  public listUnit: any[] = [];
  public listPosition: any[] = [];
  public isLoadingList = false;
  public readonly statuses = EMPLOYEE_STATUSES;
  public readonly labelOf = labelOf;
  public readonly dateFormat = DATE_FORMAT;

  constructor(
    private authenticationService: AuthenticationService,
    private employeeService: EmployeeService,
    private organizationUnitService: OrganizationUnitService,
    private positionService: PositionService,
    private formBuilder: UntypedFormBuilder,
    private notify: NotifyService,
    private modalService: ModalService,
    private router: Router,
    private renderer: Renderer2
  ) {}

  public ngOnInit(): void {
    const listMenu = this.authenticationService.userValue?.listMenu;
    checkClassActive(this.router, this.renderer, listMenu);
    this.permission = permission(this.router.url, listMenu);
    this.searchForm = this.formBuilder.group({
      searchText: [STRING_EMPTY],
      organizationUnitId: [null],
      positionId: [null],
      status: [STRING_EMPTY],
    });
    if (this.permission?.allowAccess) {
      this.getLists();
      this.getData();
    }
  }

  private getLists(): void {
    this.isLoadingList = true;
    this.organizationUnitService.getAllData().subscribe((data) => {
      if (data.responseCode === 200) {
        this.listUnit = (data.listData ?? [])
          .map((x: any) => ({ ...x, label: x.code ? `${x.code} – ${x.name}` : x.name }))
          .sort((a: any, b: any) => (a.code ?? '').localeCompare(b.code ?? ''));
      }
    });
    this.positionService.getAllData().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.listPosition = data.listData ?? [];
        }
      },
      () => {
        this.isLoadingList = false;
      },
      () => {
        this.isLoadingList = false;
      }
    );
  }

  private getData(): void {
    this.isLoading = true;
    const { searchText, organizationUnitId, positionId, status } = this.searchForm.value;
    const param = {
      ...this.table.toParams(),
      searchText: searchText ?? STRING_EMPTY,
      organizationUnitId: organizationUnitId ?? STRING_EMPTY,
      positionId: positionId ?? STRING_EMPTY,
      status: status ?? STRING_EMPTY,
    };
    this.employeeService.getAll(param).subscribe(
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

  public setStatus(status: string): void {
    this.searchForm.patchValue({ status });
    this.search();
  }

  public resetSearch(): void {
    this.searchForm.reset({ searchText: STRING_EMPTY, organizationUnitId: null, positionId: null, status: STRING_EMPTY });
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
      .openForResult(EmployeeModalComponent, {
        title: data ? UPDATE_MODAL_TITLE : ADD_MODAL_TITLE,
        data,
        size: 'lg',
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.getData();
        }
      });
  }

  public async delete(id: any): Promise<void> {
    if (!(await this.notify.confirmDelete())) {
      return;
    }
    this.isLoading = true;
    this.employeeService.delete(id).subscribe(
      (result) => {
        if (result.responseCode === 200) {
          this.notify.success(result.responseMessage);
          if (this.listData.length === 1 && this.table.page > 1) {
            this.table.page--;
          }
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
