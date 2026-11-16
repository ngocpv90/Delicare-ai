import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { checkValidateBetweenTwoDate, toDateParam } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, CodeLabel, toNumberParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { PricePolicyService } from 'src/app/services/price-policy.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// PricePolicyRequest: CustomerId, ContractId, Code, Name, UnitPrice (> 0), CurrencyCode, EffectiveFrom (NOT NULL),
// EffectiveTo, Status, MealType. Backend blocks an Active policy overlapping another Active one with the same
// customer / contract / meal type. Status Active / Inactive (PROPOSED – BA/PO CONFIRM); MealType from api/MetaData.
@Component({
  selector: 'app-price-policy-modal',
  templateUrl: './price-policy-modal.component.html',
})
export class PricePolicyModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public mealTypes: CodeLabel[] = [];
  public listCustomer: any[] = [];
  public listContractAll: any[] = [];
  public listContract: any[] = [];
  protected requestFields = [
    'customerId', 'contractId', 'code', 'name', 'unitPrice', 'currencyCode', 'effectiveFrom', 'effectiveTo', 'status', 'mealType',
  ];

  constructor(
    protected api: PricePolicyService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      customerId: [null, [Validators.required]],
      contractId: [null],
      mealType: [null],
      code: ['', [Validators.maxLength(50)]],
      name: [''],
      unitPrice: [null, [Validators.required, Validators.min(1)]],
      currencyCode: ['VND'],
      effectiveFrom: [null, [Validators.required]],
      effectiveTo: [null],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getReferenceList('MealType').subscribe((list) => (this.mealTypes = list));
    forkJoin([this.lookup.customers(), this.lookup.contracts()]).subscribe(([customers, contracts]) => {
      this.listCustomer = customers;
      this.listContractAll = contracts;
      this.filterContracts(this.formGroup.value.customerId, false);
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue(record);
    this.filterContracts(record.customerId, false);
  }

  public customerChanged(customerId: any): void {
    this.filterContracts(customerId, true);
  }

  // Contracts of the chosen customer only
  private filterContracts(customerId: any, reset: boolean): void {
    const id = String(customerId ?? '').toLowerCase();
    this.listContract = id ? this.listContractAll.filter((x) => String(x.customerId).toLowerCase() === id) : [];
    if (reset) {
      this.formGroup.patchValue({ contractId: null });
    }
  }

  protected override validate(v: any): string | null {
    if (checkValidateBetweenTwoDate(v.effectiveFrom, v.effectiveTo)) {
      return 'Ngày hết hiệu lực phải sau ngày bắt đầu hiệu lực';
    }
    return null;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      currencyCode: (v.currencyCode ?? '').trim() || 'VND',
      unitPrice: toNumberParam(v.unitPrice),
      effectiveFrom: toDateParam(v.effectiveFrom),
      effectiveTo: toDateParam(v.effectiveTo),
    };
  }
}
