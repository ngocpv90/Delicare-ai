import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, labelOf } from 'src/app/helpers/master-data-options';
import { ContractMealService } from 'src/app/services/contract-meal.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { ContractMealModalComponent } from '../contract/contract-meal-modal/contract-meal-modal.component';

// crm-contract-meal – Suất ăn theo hợp đồng (FE-2): danh sách tất cả suất của mọi hợp đồng (menu riêng trong seed,
// PermissionConvention maps ContractMeal to this screen). Same modal as the crm-contract drawer.
@Component({
  selector: 'app-contract-meal',
  templateUrl: './contract-meal.component.html',
})
export class ContractMealComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public mealTypes: CodeLabel[] = [];
  public listContract: any[] = [];
  protected searchFields = ['mealName', 'contractName', 'customerName', 'serviceType'];
  protected override filterDefaults = { contractId: null, mealType: null, status: '' };

  constructor(
    private contractMealService: ContractMealService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.metaDataService.getReferenceList('MealType').subscribe((list) => (this.mealTypes = list));
  }

  protected loadRows() {
    return forkJoin([
      this.contractMealService.getAllData(),
      this.lookup.contracts(),
      this.lookup.customers(),
      this.lookup.units(),
    ]).pipe(
      map(([res, contracts, customers, units]) => {
        this.listContract = contracts;
        const contractNames = labelMap(contracts, (x) => x.label);
        const customerNames = labelMap(customers);
        const unitNames = labelMap(units, (x) => x.label);
        const contractCustomer = new Map(contracts.map((x) => [String(x.id).toLowerCase(), x.customerId]));
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          contractName: labelFrom(contractNames, x.contractId),
          customerName: labelFrom(customerNames, contractCustomer.get(String(x.contractId).toLowerCase())),
          unitName: labelFrom(unitNames, x.unitId),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.contractMealService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(ContractMealModalComponent, data ?? { contractId: this.searchForm.value.contractId });
  }
}
