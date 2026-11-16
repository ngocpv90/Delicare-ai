import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { toDateParam } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, CodeLabel, PRICE_SOURCES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { PricePolicyService } from 'src/app/services/price-policy.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { PricePolicyModalComponent } from './price-policy-modal/price-policy-modal.component';

// crm-price-policy – Chính sách giá theo KH / HĐ (FE-3). PricePolicy/GetAllData + tên khách / hợp đồng; hộp "Tra giá"
// gọi PricePolicy/Resolve (thứ tự: chính sách theo hợp đồng → theo khách → đơn giá ContractMeal).
// Status Active / Inactive (PROPOSED – BA/PO CONFIRM); MealType từ api/MetaData/GetReferenceLists.
@Component({
  selector: 'app-price-policy',
  templateUrl: './price-policy.component.html',
})
export class PricePolicyComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public mealTypes: CodeLabel[] = [];
  public listCustomer: any[] = [];
  private listContractAll: any[] = [];
  protected searchFields = ['code', 'name', 'customerName', 'contractName'];
  protected override filterDefaults = { customerId: null, status: '' };

  // "Tra giá" box
  public tryCustomerId: any = null;
  public tryContractId: any = null;
  public tryMealType: any = null;
  public tryDate: any = null;
  public tryContracts: any[] = [];
  public tryResult: any = null;
  public tryError = '';
  public isResolving = false;

  constructor(
    private pricePolicyService: PricePolicyService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.metaDataService.getReferenceList('MealType').subscribe((list) => (this.mealTypes = list));
  }

  protected loadRows() {
    return forkJoin([this.pricePolicyService.getAllData(), this.lookup.customers(), this.lookup.contracts()]).pipe(
      map(([res, customers, contracts]) => {
        this.listCustomer = customers;
        this.listContractAll = contracts;
        const customerNames = labelMap(customers);
        const contractNames = labelMap(contracts, (x) => x.label);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          customerName: labelFrom(customerNames, x.customerId),
          contractName: labelFrom(contractNames, x.contractId),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.pricePolicyService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(PricePolicyModalComponent, data ?? { customerId: this.searchForm.value.customerId });
  }

  public sourceLabel(source?: string | null): string {
    return labelOf(PRICE_SOURCES, source);
  }

  public tryCustomerChanged(customerId: any): void {
    const id = String(customerId ?? '').toLowerCase();
    this.tryContracts = id ? this.listContractAll.filter((x) => String(x.customerId).toLowerCase() === id) : [];
    this.tryContractId = null;
    this.tryResult = null;
    this.tryError = '';
  }

  public resolve(): void {
    this.tryResult = null;
    this.tryError = '';
    if (!this.tryCustomerId) {
      this.tryError = 'Chọn khách hàng';
      return;
    }
    this.isResolving = true;
    this.pricePolicyService
      .resolve({
        customerId: this.tryCustomerId,
        contractId: this.tryContractId,
        mealType: this.tryMealType,
        date: toDateParam(this.tryDate),
      })
      .subscribe({
        next: (res) => {
          this.isResolving = false;
          if (res?.responseCode === 200 && res.data) {
            this.tryResult = res.data;
          } else {
            this.tryError = res?.responseMessage || 'Không tìm thấy đơn giá áp dụng';
          }
        },
        error: () => {
          this.isResolving = false;
          this.notify.error();
        },
      });
  }
}
