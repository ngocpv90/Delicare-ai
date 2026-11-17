import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { menuUrl, permission } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, CodeLabel, labelOf } from 'src/app/helpers/master-data-options';
import { ContractMealService } from 'src/app/services/contract-meal.service';
import { ContractService } from 'src/app/services/contract.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { ContractMealModalComponent } from './contract-meal-modal/contract-meal-modal.component';
import { ContractModalComponent } from './contract-modal/contract-modal.component';

// crm-contract – Hợp đồng khách hàng (FE-2) + ngăn kéo: thông tin, suất ăn (CRUD ContractMeal, quyền theo menu
// #/crm-contract-meal – PermissionConvention maps ContractMeal to that screen).
@Component({
  selector: 'app-contract',
  templateUrl: './contract.component.html',
})
export class ContractComponent extends MasterListBase {
  public readonly labelOf = labelOf;
  public readonly recordStatuses = ACTIVE_STATUSES;
  public statuses: CodeLabel[] = [];
  public mealTypes: CodeLabel[] = [];
  public listCustomer: any[] = [];
  public mealPermission: any;
  protected searchFields = ['contractNumber', 'name', 'customerName', 'paymentTerm'];
  protected override filterDefaults = { customerId: null, status: '' };

  // Drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'meal';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'meal', label: 'Suất ăn & đơn giá' },
    { key: 'info', label: 'Thông tin hợp đồng' },
  ];
  public meals: any[] = [];
  public isLoadingMeal = false;
  private unitNames = new Map<string, string>();

  constructor(
    private contractService: ContractService,
    private contractMealService: ContractMealService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.mealPermission = permission(menuUrl('#/crm-contract-meal'), this.authenticationService.userValue?.listMenu);
    this.metaDataService.getStatusSet('Contract.Status').subscribe((list) => (this.statuses = list));
    this.metaDataService.getReferenceList('MealType').subscribe((list) => (this.mealTypes = list));
  }

  protected loadRows() {
    return forkJoin([
      this.contractService.getAllData(),
      this.lookup.customers(),
      this.lookup.list('ContractMeal'),
      this.lookup.units(),
    ]).pipe(
      map(([res, customers, meals, units]) => {
        this.listCustomer = customers;
        this.unitNames = labelMap(units, (x) => x.label);
        const names = labelMap(customers);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          customerName: labelFrom(names, x.customerId),
          effectiveFrom: x.effectiveFrom ?? x.startDate,
          effectiveTo: x.effectiveTo ?? x.endDate,
          mealCount: meals.filter((m) => String(m.contractId).toLowerCase() === String(x.id).toLowerCase()).length,
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.contractService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(ContractModalComponent, data ?? { customerId: this.searchForm.value.customerId }, 'lg');
  }

  public openDrawer(row: any): void {
    this.drawerRow = row;
    this.drawerTab = 'meal';
    this.drawerOpen = true;
    this.loadMeals();
  }

  // ContractMeal/GetAll?ContractId= works (ContractId is the filter the drawer always sends)
  private loadMeals(): void {
    this.meals = [];
    this.isLoadingMeal = true;
    this.contractMealService.getAll({ contractId: this.drawerRow.id, page: 1, pageSize: 1000 }).subscribe({
      next: (res) => {
        this.isLoadingMeal = false;
        this.meals = (res?.responseCode === 200 ? res.listData ?? [] : []).map((x: any) => ({
          ...x,
          unitName: x.unitName || labelFrom(this.unitNames, x.unitId),
        }));
      },
      error: () => {
        this.isLoadingMeal = false;
        this.notify.error();
      },
    });
  }

  public editFromDrawer(): void {
    this.drawerOpen = false;
    this.openModal(this.drawerRow);
  }

  public openMealModal(meal?: any): void {
    this.modalService
      .openForResult(ContractMealModalComponent, {
        title: meal ? 'Cập nhật suất ăn' : 'Thêm suất ăn',
        data: meal ?? { contractId: this.drawerRow.id, lockContract: true },
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.loadMeals();
          this.reload();
        }
      });
  }

  public async deleteMeal(id: any): Promise<void> {
    if (!(await this.notify.confirmDelete())) {
      return;
    }
    this.isLoadingMeal = true;
    this.contractMealService.delete(id).subscribe({
      next: (res) => {
        this.isLoadingMeal = false;
        if (res?.responseCode === 200) {
          this.notify.success(res.responseMessage);
          this.loadMeals();
          this.reload();
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: () => {
        this.isLoadingMeal = false;
        this.notify.error();
      },
    });
  }
}
