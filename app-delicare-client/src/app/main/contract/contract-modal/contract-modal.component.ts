import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { checkValidateBetweenTwoDate, toDateParam } from 'src/app/helpers/common-helper';
import { CodeLabel, toNumberParam } from 'src/app/helpers/master-data-options';
import { ContractService } from 'src/app/services/contract.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// ContractRequest: CustomerId, ContractNumber, Name, SignedDate, EffectiveFrom (NOT NULL), EffectiveTo, ContractValue,
// Status, Note, StartDate, EndDate, PaymentTerm. StartDate / EndDate (read by Customer/GetOverview) are written with
// the same values as EffectiveFrom / EffectiveTo. Status: Contract.Status (Nháp → Chờ duyệt → Hiệu lực …, PROPOSED);
// the approval flow itself comes with the approval inbox (FE-P2).
@Component({
  selector: 'app-contract-modal',
  templateUrl: './contract-modal.component.html',
})
export class ContractModalComponent extends MasterModalBase {
  public statuses: CodeLabel[] = [];
  public listCustomer: any[] = [];
  protected requestFields = [
    'customerId', 'contractNumber', 'name', 'signedDate', 'effectiveFrom', 'effectiveTo', 'contractValue', 'status',
    'note', 'startDate', 'endDate', 'paymentTerm',
  ];

  constructor(
    protected api: ContractService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      customerId: [null, [Validators.required]],
      contractNumber: ['', [Validators.required]],
      name: ['', [Validators.required]],
      signedDate: [null],
      effectiveFrom: [null, [Validators.required]],
      effectiveTo: [null],
      contractValue: [null, [Validators.min(0)]],
      paymentTerm: [''],
      status: ['Draft', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getStatusSet('Contract.Status').subscribe((list) => (this.statuses = list));
    this.lookup.customers().subscribe((list) => {
      this.listCustomer = list;
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue({
      ...record,
      effectiveFrom: record.effectiveFrom ?? record.startDate,
      effectiveTo: record.effectiveTo ?? record.endDate,
    });
  }

  protected override validate(v: any): string | null {
    if (checkValidateBetweenTwoDate(v.effectiveFrom, v.effectiveTo)) {
      return 'Ngày hết hiệu lực phải sau ngày bắt đầu hiệu lực';
    }
    return null;
  }

  protected override toParams(v: any) {
    const effectiveFrom = toDateParam(v.effectiveFrom);
    const effectiveTo = toDateParam(v.effectiveTo);
    return {
      ...v,
      contractNumber: (v.contractNumber ?? '').trim(),
      name: (v.name ?? '').trim(),
      signedDate: toDateParam(v.signedDate),
      effectiveFrom,
      effectiveTo,
      startDate: effectiveFrom,
      endDate: effectiveTo,
      contractValue: toNumberParam(v.contractValue),
    };
  }
}
